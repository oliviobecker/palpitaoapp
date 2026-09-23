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
using Palpitao.Api.Services.Localization;

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
    private readonly IOcrAliasService _aliases;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly OcrStorageOptions _storage;
    private readonly ILocalizationService _localization;
    private readonly ILogger<OcrService> _logger;

    public OcrService(
        AppDbContext db,
        IOcrEngine engine,
        IPredictionImportService import,
        IOcrAliasService aliases,
        IAuditService audit,
        ICurrentGroupService current,
        IOptions<OcrStorageOptions> storage,
        ILocalizationService localization,
        ILogger<OcrService> logger)
    {
        _db = db;
        _engine = engine;
        _import = import;
        _aliases = aliases;
        _audit = audit;
        _current = current;
        _storage = storage.Value;
        _localization = localization;
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

        // Up front, like the language check below: a finalized round would only fail at
        // confirm, after the admin reviewed every candidate — and would leave a batch behind.
        AdminPredictionWindow.EnsureOpen(round);

        var now = DateTime.UtcNow;
        var lang = NormalizeLanguage(language);

        // Before the batch, not inside the try below: a server without its language models is a
        // deployment fault, not a failed import, so it should leave no Failed batch and no stored
        // image behind. (It also could not use the catch — the `when` filter skips
        // BusinessRuleException, so the batch would never be saved but would still be tracked.)
        var missingLanguages = _engine.MissingLanguages(lang);
        if (missingLanguages.Count > 0)
        {
            throw new BusinessRuleException("ocr.tessdataMissing");
        }

        var batch = new OcrImportBatch
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            UploadedByUserId = adminId,
            // The name comes from the client and the column has a width.
            OriginalFileName = fileName.Length > OcrImportBatch.MaxOriginalFileNameLength
                ? fileName[..OcrImportBatch.MaxOriginalFileNameLength]
                : fileName,
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

        OcrImportResult result;
        try
        {
            var readings = _engine.ReadVariants(bytes, lang);
            // Kept even if building the candidates fails below, so a failed batch still shows what
            // OCR saw; replaced by the reading actually chosen once there is one.
            batch.ExtractedText = readings.FirstOrDefault()?.Text;
            batch.Status = OcrBatchStatus.Processed;
            batch.ProcessedAt = now;

            var participants = await GroupQueries.ActiveParticipants(_db, groupId)
                .ToListAsync(ct);
            var aliases = await _aliases.GetForGroupAsync(groupId, ct);
            var catalogue = await _db.Teams.AsNoTracking().Select(t => t.Name).ToListAsync(ct);
            var otherRounds = await _db.RoundMatches
                .AsNoTracking()
                .Include(m => m.Round)
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.Round!.SeasonId == round.SeasonId
                    && m.Round.GroupId == groupId
                    && m.RoundId != roundId)
                .ToListAsync(ct);

            result = _import.BuildCandidates(
                batch.Id,
                roundId,
                readings,
                new OcrImportContext(
                    round.Matches.ToList(),
                    participants,
                    aliases,
                    fileName,
                    catalogue,
                    otherRounds,
                    _localization.Language));
            batch.ExtractedText = result.Reading.Text;
            _db.OcrPredictionCandidates.AddRange(result.Candidates);

            _logger.LogInformation(
                "OCR: leitura {Variant} escolhida para {File}: {Candidates} linhas, {Ignored} de outra rodada ignoradas.",
                result.Reading.Variant,
                fileName,
                result.Candidates.Count,
                result.IgnoredLineCount);
            _audit.Add(adminId, "OcrImportProcessed", nameof(OcrImportBatch), batch.Id.ToString(),
                new
                {
                    roundId,
                    candidates = result.Candidates.Count,
                    variant = result.Reading.Variant,
                    ignoredOtherRound = result.IgnoredLineCount,
                    ignoredRounds = result.IgnoredRoundLabels,
                    imageBytes = bytes.Length,
                    contentType,
                });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not BusinessRuleException and not NotFoundException)
        {
            _logger.LogError(ex, "Falha ao processar OCR para a rodada {RoundId} (idioma {Language})", roundId, lang);
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

        var dto = await GetBatchAsync(batch.Id, ct);
        // Not stored: the lines were never candidates. The upload response is where the admin needs
        // to hear about them; the extracted text still shows them on a reload.
        dto.IgnoredLineCount = result.IgnoredLineCount;
        dto.IgnoredRoundLabels = result.IgnoredRoundLabels.ToList();
        return dto;
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

        var dto = Map(batch, hasImage);
        if (IsUnderReview(batch.Status))
        {
            dto.Overwrites = await OverwritesAsync(batch, ct);
        }
        return dto;
    }

    private static bool IsUnderReview(OcrBatchStatus status)
        => status is OcrBatchStatus.Processed or OcrBatchStatus.Reviewed;

    /// <summary>A complete candidate row: the only kind a confirm writes.</summary>
    private sealed record CandidateRow(Guid BatchId, Guid UserId, Guid RoundMatchId, int Home, int Away);

    /// <summary>
    /// Counts, per batch and participant, the rows that would replace one of the participant's
    /// predictions in the round with a different score — what a confirm overwrites without asking.
    /// A row that restates the stored score (the same screenshot imported again) or fills a match
    /// the participant has no prediction for changes nothing and is not counted.
    /// </summary>
    private async Task<(Dictionary<(Guid BatchId, Guid UserId), int> Changed, Dictionary<Guid, int> Existing)>
        CountOverwritesAsync(Guid roundId, IReadOnlyCollection<CandidateRow> rows, CancellationToken ct)
    {
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return (new(), new());
        }

        var stored = await _db.Predictions
            .AsNoTracking()
            .Where(p => p.RoundId == roundId && userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.RoundMatchId, p.PredictedHomeScore, p.PredictedAwayScore })
            .ToListAsync(ct);
        // (RoundMatchId, UserId) is unique on Predictions.
        var byKey = stored.ToDictionary(p => (p.UserId, p.RoundMatchId));

        var changed = rows
            .Where(r => byKey.TryGetValue((r.UserId, r.RoundMatchId), out var p)
                && (p.PredictedHomeScore != r.Home || p.PredictedAwayScore != r.Away))
            .GroupBy(r => (r.BatchId, r.UserId))
            .ToDictionary(g => g.Key, g => g.Count());
        var existing = stored.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Count());
        return (changed, existing);
    }

    private async Task<List<OcrOverwriteDto>> OverwritesAsync(OcrImportBatch batch, CancellationToken ct)
    {
        var rows = batch.Candidates
            .Where(c => c.UserId is not null && c.RoundMatchId is not null
                && c.PredictedHomeScore is not null && c.PredictedAwayScore is not null)
            .Select(c => new CandidateRow(batch.Id, c.UserId!.Value, c.RoundMatchId!.Value,
                c.PredictedHomeScore!.Value, c.PredictedAwayScore!.Value))
            .ToList();
        var (changed, existing) = await CountOverwritesAsync(batch.RoundId, rows, ct);
        if (changed.Count == 0)
        {
            return new List<OcrOverwriteDto>();
        }

        var userIds = changed.Keys.Select(k => k.UserId).ToList();
        var names = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return changed
            .Select(kv => new OcrOverwriteDto
            {
                UserId = kv.Key.UserId,
                UserName = names.GetValueOrDefault(kv.Key.UserId) ?? string.Empty,
                ExistingCount = existing.GetValueOrDefault(kv.Key.UserId),
                ChangedCount = kv.Value,
            })
            .OrderBy(o => o.UserName)
            .ToList();
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
                NeedsReviewCount = b.Candidates.Count(c => c.NeedsReview),
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

        // Who each batch is filed against, for the pending list the multi-image upload shows, and
        // what its rows would write. Only these columns, and only for batches already scoped to
        // the group above.
        var batchIds = batches.Select(b => b.Id).ToList();
        var candidates = await _db.OcrPredictionCandidates
            .AsNoTracking()
            .Where(c => batchIds.Contains(c.OcrImportBatchId))
            .Select(c => new
            {
                c.OcrImportBatchId,
                c.UserId,
                c.RoundMatchId,
                c.PredictedHomeScore,
                c.PredictedAwayScore,
            })
            .ToListAsync(ct);
        var ownerByBatch = candidates
            .GroupBy(o => o.OcrImportBatchId)
            .ToDictionary(g => g.Key, g => g.Select(o => o.UserId).Distinct().ToList());

        // Only a batch still under review can overwrite anything: a confirmed one already did.
        var underReview = batches.Where(b => IsUnderReview(b.Status)).Select(b => b.Id).ToHashSet();
        var rows = candidates
            .Where(c => underReview.Contains(c.OcrImportBatchId)
                && c.UserId is not null && c.RoundMatchId is not null
                && c.PredictedHomeScore is not null && c.PredictedAwayScore is not null)
            .Select(c => new CandidateRow(c.OcrImportBatchId, c.UserId!.Value, c.RoundMatchId!.Value,
                c.PredictedHomeScore!.Value, c.PredictedAwayScore!.Value))
            .ToList();
        var (changed, _) = await CountOverwritesAsync(roundId, rows, ct);
        var changedByBatch = changed
            .GroupBy(kv => kv.Key.BatchId)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        foreach (var batch in batches)
        {
            batch.UploadedByName = names.GetValueOrDefault(batch.UploadedByUserId);
            batch.ParticipantUserId = ownerByBatch.TryGetValue(batch.Id, out var users) && users.Count == 1
                ? users[0]
                : null;
            batch.OverwriteCount = changedByBatch.GetValueOrDefault(batch.Id);
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

        // The import's notes are about the fixture and the score it read (readings that disagree, a
        // score read off a letter, an approximate match). Touching either answers them, so the
        // note goes; filing the row under a participant — what "apply to all" does to every card —
        // does not, and must not quietly clear a doubt about the score. A note the client itself
        // changed is taken as sent.
        var readingReviewed = candidate.RoundMatchId != request.RoundMatchId
            || candidate.PredictedHomeScore != request.PredictedHomeScore
            || candidate.PredictedAwayScore != request.PredictedAwayScore;
        var notes = request.ReviewNotes != candidate.ReviewNotes
            ? request.ReviewNotes
            : readingReviewed ? null : candidate.ReviewNotes;

        candidate.UserId = request.UserId;
        candidate.RoundMatchId = request.RoundMatchId;
        candidate.PredictedHomeScore = request.PredictedHomeScore;
        candidate.PredictedAwayScore = request.PredictedAwayScore;
        candidate.ReviewNotes = notes;
        candidate.NeedsReview = request.UserId is null || request.RoundMatchId is null
            || request.PredictedHomeScore is null || request.PredictedAwayScore is null
            || request.PredictedHomeScore < 0 || request.PredictedAwayScore < 0
            || (notes is not null && !readingReviewed);
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

        batch.Status = OcrBatchStatus.Cancelled;
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
