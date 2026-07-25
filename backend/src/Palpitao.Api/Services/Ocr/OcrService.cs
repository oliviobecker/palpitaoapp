using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Ocr;

public class OcrService : IOcrService
{
    /// <summary>Validation limit for the uploaded image (the request-size limit adds multipart headroom).</summary>
    public const long MaxImageBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp",
    };

    private readonly AppDbContext _db;
    private readonly IOcrEngine _engine;
    private readonly IPredictionImportService _import;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly OcrStorageOptions _storage;
    private readonly ILogger<OcrService> _logger;

    public OcrService(
        AppDbContext db,
        IOcrEngine engine,
        IPredictionImportService import,
        IAuditService audit,
        ICurrentGroupService current,
        IOptions<OcrStorageOptions> storage,
        ILogger<OcrService> logger)
    {
        _db = db;
        _engine = engine;
        _import = import;
        _audit = audit;
        _current = current;
        _storage = storage.Value;
        _logger = logger;
    }

    /// <summary>Ensures the OCR batch belongs to a round in the current group (else 404).</summary>
    private async Task EnsureBatchInGroupAsync(Guid batchId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var inGroup = await _db.OcrImportBatches
            .AnyAsync(b => b.Id == batchId && b.Round!.GroupId == groupId, ct);
        if (!inGroup)
        {
            throw new NotFoundException("notFound.ocrBatch");
        }
    }

    /// <summary>Validates the uploaded file (extension + size).</summary>
    public static void ValidateFile(string fileName, long length)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new BusinessRuleException("ocr.invalidFormat");
        }

        if (length <= 0)
        {
            throw new BusinessRuleException("ocr.emptyFile");
        }

        if (length > MaxImageBytes)
        {
            throw new BusinessRuleException("ocr.tooLarge");
        }
    }

    /// <summary>
    /// Sniffs the real image type from the header and rejects a file whose content is not a
    /// supported image, or whose content disagrees with its own extension. <see cref="ValidateFile"/>
    /// only sees the file name; this is what stops arbitrary bytes from being stored and later
    /// served back from the API origin.
    /// </summary>
    public static (string ContentType, string Extension) ValidateContent(string fileName, byte[] bytes)
    {
        if (!ImageContentType.TryDetect(bytes, out var contentType, out var extension))
        {
            throw new BusinessRuleException("ocr.contentMismatch");
        }

        // ".jpeg" and ".jpg" are the same format; everything else must match the sniffed type.
        var claimed = Path.GetExtension(fileName);
        var claimedCanonical = claimed.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : claimed;
        if (!claimedCanonical.Equals(extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("ocr.contentMismatch");
        }

        return (contentType, extension);
    }

    public async Task<OcrBatchDto> ProcessAsync(
        Guid roundId, string fileName, byte[] bytes, string? language, Guid adminId, CancellationToken ct)
    {
        ValidateFile(fileName, bytes.Length);
        var (contentType, extension) = ValidateContent(fileName, bytes);

        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        var now = DateTime.UtcNow;
        var lang = NormalizeLanguage(language);

        var batch = new OcrImportBatch
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            UploadedByUserId = adminId,
            OriginalFileName = fileName,
            LanguageUsed = lang,
            Status = OcrBatchStatus.Uploaded,
            CreatedAt = now,
        };
        _db.OcrImportBatches.Add(batch);

        // Queued before the try on purpose: the catch below saves a Failed batch, so the image
        // is kept even when OCR blows up — which is exactly when an admin needs to see it.
        if (_storage.StoreImages)
        {
            _db.OcrImportImages.Add(new OcrImportImage
            {
                OcrImportBatchId = batch.Id,
                Content = bytes,
                ContentType = contentType,
                FileExtension = extension,
                ByteSize = bytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                CreatedAt = now,
            });
        }

        try
        {
            var text = _engine.ExtractText(bytes, lang);
            batch.ExtractedText = text;
            batch.Status = OcrBatchStatus.Processed;
            batch.ProcessedAt = now;

            var participants = await GroupQueries.ActiveParticipants(_db, groupId)
                .ToListAsync(ct);

            var candidates = _import.BuildCandidates(batch.Id, roundId, text, round.Matches.ToList(), participants);
            _db.OcrPredictionCandidates.AddRange(candidates);

            _audit.Add(adminId, "OcrImportProcessed", nameof(OcrImportBatch), batch.Id.ToString(),
                new { roundId, candidates = candidates.Count, imageBytes = bytes.Length, contentType });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not BusinessRuleException and not NotFoundException)
        {
            _logger.LogError(ex, "Falha ao processar OCR para a rodada {RoundId}", roundId);
            batch.Status = OcrBatchStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException("ocr.processFailed");
        }
        finally
        {
            // In the finally, not after the try: the catch above persists the image of a failed
            // batch and rethrows, so pruning only on the success path would let a round whose OCR
            // keeps failing grow past MaxImagesPerRound. Cannot throw (it swallows and logs), so
            // it never masks the original exception.
            await PruneRoundImagesAsync(roundId, ct);
        }

        return await GetBatchAsync(batch.Id, ct);
    }

    /// <summary>
    /// Drops the bytes of the oldest uploads beyond <see cref="OcrStorageOptions.MaxImagesPerRound"/>.
    /// Only the images go — the batches, their candidates and the audit trail survive. Never allowed
    /// to fail the upload that triggered it.
    /// </summary>
    private async Task PruneRoundImagesAsync(Guid roundId, CancellationToken ct)
    {
        if (_storage.MaxImagesPerRound <= 0)
        {
            return;
        }

        try
        {
            // Project the keys only: materialising the entities would SELECT the bytea of every
            // image about to be deleted (up to 10 MB each), which is exactly what the side table
            // exists to avoid. CreatedAt alone is not a total order — two uploads can share a
            // tick — so the key breaks ties and keeps the window deterministic.
            var staleIds = await _db.OcrImportImages
                .Where(i => i.Batch!.RoundId == roundId)
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.OcrImportBatchId)
                .Skip(_storage.MaxImagesPerRound)
                .Select(i => i.OcrImportBatchId)
                .ToListAsync(ct);

            if (staleIds.Count == 0)
            {
                return;
            }

            // Set-based delete: no change-tracker entries left in a Deleted state if it fails.
            var removed = await _db.OcrImportImages
                .Where(i => staleIds.Contains(i.OcrImportBatchId))
                .ExecuteDeleteAsync(ct);
            _logger.LogInformation(
                "Removidas {Count} imagens antigas de OCR da rodada {RoundId}.", removed, roundId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao podar imagens de OCR da rodada {RoundId}", roundId);
        }
    }

    public async Task<OcrBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct)
    {
        await EnsureBatchInGroupAsync(batchId, ct);
        // Read-only projection to a DTO: no tracking needed.
        var batch = await _db.OcrImportBatches
            .AsNoTracking()
            .Include(b => b.Candidates)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        // Deliberately a separate existence check rather than Include(b => b.Image): including it
        // would pull the whole blob on a path the review screen hits after every edit.
        var hasImage = await _db.OcrImportImages.AnyAsync(i => i.OcrImportBatchId == batchId, ct);

        return Map(batch, hasImage);
    }

    public async Task<List<OcrBatchSummaryDto>> ListBatchesAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var roundInGroup = await _db.Rounds.AnyAsync(r => r.Id == roundId && r.GroupId == groupId, ct);
        if (!roundInGroup)
        {
            throw new NotFoundException("notFound.round");
        }

        // Projection only: materialising the entity would drag OcrImportImage into scope.
        var batches = await _db.OcrImportBatches
            .AsNoTracking()
            .Where(b => b.RoundId == roundId && b.Round!.GroupId == groupId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new OcrBatchSummaryDto
            {
                Id = b.Id,
                RoundId = b.RoundId,
                Status = b.Status,
                OriginalFileName = b.OriginalFileName,
                LanguageUsed = b.LanguageUsed,
                HasImage = b.Image != null,
                ImageContentType = b.Image!.ContentType,
                ImageByteSize = (int?)b.Image!.ByteSize,
                CandidateCount = b.Candidates.Count,
                UploadedByUserId = b.UploadedByUserId,
                CreatedAt = b.CreatedAt,
                ProcessedAt = b.ProcessedAt,
                ConfirmedAt = b.ConfirmedAt,
            })
            .ToListAsync(ct);

        // UploadedByUserId has no FK/navigation, so the name is resolved separately (and stays
        // null for a user that no longer exists).
        var uploaderIds = batches.Select(b => b.UploadedByUserId).Distinct().ToList();
        var names = await _db.Users
            .AsNoTracking()
            .Where(u => uploaderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        foreach (var batch in batches)
        {
            batch.UploadedByName = names.GetValueOrDefault(batch.UploadedByUserId);
        }

        return batches;
    }

    public async Task<OcrImageContent> GetImageAsync(Guid batchId, CancellationToken ct)
    {
        await EnsureBatchInGroupAsync(batchId, ct);

        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.OcrImportImages
            .AsNoTracking()
            // Redundant with EnsureBatchInGroupAsync on purpose: this is the one query that emits
            // bytes, so it carries its own tenant predicate.
            .Where(i => i.OcrImportBatchId == batchId && i.Batch!.Round!.GroupId == groupId)
            .Select(i => new OcrImageContent(i.Content, i.ContentType, i.Sha256, i.CreatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("notFound.ocrImage");
    }

    public async Task<OcrBatchDto> UpdateCandidateAsync(
        Guid batchId, Guid candidateId, UpdateOcrCandidateRequest request, Guid adminId, CancellationToken ct)
    {
        var batch = await GetEditableBatchAsync(batchId, ct);
        var candidate = await _db.OcrPredictionCandidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && c.OcrImportBatchId == batchId, ct)
            ?? throw new NotFoundException("notFound.ocrCandidate");

        candidate.UserId = request.UserId;
        candidate.RoundMatchId = request.RoundMatchId;
        candidate.PredictedHomeScore = request.PredictedHomeScore;
        candidate.PredictedAwayScore = request.PredictedAwayScore;
        candidate.ReviewNotes = request.ReviewNotes;
        candidate.NeedsReview = request.UserId is null || request.RoundMatchId is null
            || request.PredictedHomeScore is null || request.PredictedAwayScore is null
            || request.PredictedHomeScore < 0 || request.PredictedAwayScore < 0;
        candidate.Confidence = (request.UserId is not null ? 0.5 : 0.0)
            + (request.RoundMatchId is not null ? 0.5 : 0.0);
        candidate.UpdatedAt = DateTime.UtcNow;

        MarkReviewed(batch);
        await _db.SaveChangesAsync(ct);
        return await GetBatchAsync(batchId, ct);
    }

    public async Task<OcrBatchDto> DeleteCandidateAsync(
        Guid batchId, Guid candidateId, Guid adminId, CancellationToken ct)
    {
        var batch = await GetEditableBatchAsync(batchId, ct);
        var candidate = await _db.OcrPredictionCandidates
            .FirstOrDefaultAsync(c => c.Id == candidateId && c.OcrImportBatchId == batchId, ct)
            ?? throw new NotFoundException("notFound.ocrCandidate");

        _db.OcrPredictionCandidates.Remove(candidate);
        MarkReviewed(batch);
        _audit.Add(adminId, "OcrCandidateDeleted", nameof(OcrImportBatch), batch.Id.ToString(),
            new { candidateId, candidate.ParticipantNameRaw, candidate.MatchTextRaw });
        await _db.SaveChangesAsync(ct);
        return await GetBatchAsync(batchId, ct);
    }

    public async Task CancelAsync(Guid batchId, Guid adminId, CancellationToken ct)
    {
        var batch = await GetEditableBatchAsync(batchId, ct);

        batch.Status = OcrBatchStatus.Failed;
        _audit.Add(adminId, "OcrImportCancelled", nameof(OcrImportBatch), batch.Id.ToString(), null);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Loads a batch in the current group that can still be reviewed (not yet confirmed).</summary>
    private async Task<OcrImportBatch> GetEditableBatchAsync(Guid batchId, CancellationToken ct)
    {
        await EnsureBatchInGroupAsync(batchId, ct);
        var batch = await _db.OcrImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        if (batch.Status == OcrBatchStatus.Confirmed)
        {
            throw new BusinessRuleException("ocr.batchAlreadyConfirmed");
        }

        return batch;
    }

    private static void MarkReviewed(OcrImportBatch batch)
    {
        if (batch.Status == OcrBatchStatus.Processed)
        {
            batch.Status = OcrBatchStatus.Reviewed;
        }
    }

    private static string NormalizeLanguage(string? language) => language switch
    {
        "eng" => "eng",
        "por+eng" => "por+eng",
        "eng+por" => "por+eng",
        _ => "por",
    };

    private static OcrBatchDto Map(OcrImportBatch b, bool hasImage) => new()
    {
        Id = b.Id,
        RoundId = b.RoundId,
        Status = b.Status,
        LanguageUsed = b.LanguageUsed,
        OriginalFileName = b.OriginalFileName,
        ExtractedText = b.ExtractedText,
        HasImage = hasImage,
        CreatedAt = b.CreatedAt,
        ProcessedAt = b.ProcessedAt,
        ConfirmedAt = b.ConfirmedAt,
        Candidates = b.Candidates
            .OrderByDescending(c => c.NeedsReview)
            .ThenBy(c => c.ParticipantNameRaw)
            .Select(c => new OcrCandidateDto
            {
                Id = c.Id,
                UserId = c.UserId,
                ParticipantNameRaw = c.ParticipantNameRaw,
                RoundMatchId = c.RoundMatchId,
                MatchTextRaw = c.MatchTextRaw,
                PredictedHomeScore = c.PredictedHomeScore,
                PredictedAwayScore = c.PredictedAwayScore,
                Confidence = c.Confidence,
                NeedsReview = c.NeedsReview,
                ReviewNotes = c.ReviewNotes,
            })
            .ToList(),
    };
}
