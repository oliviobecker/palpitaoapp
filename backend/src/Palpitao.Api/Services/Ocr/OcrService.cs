using Microsoft.EntityFrameworkCore;
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
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp",
    };

    private readonly AppDbContext _db;
    private readonly IOcrEngine _engine;
    private readonly IPredictionImportService _import;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly ILogger<OcrService> _logger;

    public OcrService(
        AppDbContext db,
        IOcrEngine engine,
        IPredictionImportService import,
        IAuditService audit,
        ICurrentGroupService current,
        ILogger<OcrService> logger)
    {
        _db = db;
        _engine = engine;
        _import = import;
        _audit = audit;
        _current = current;
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

        if (length > MaxBytes)
        {
            throw new BusinessRuleException("ocr.tooLarge");
        }
    }

    public async Task<OcrBatchDto> ProcessAsync(
        Guid roundId, string fileName, byte[] bytes, string? language, Guid adminId, CancellationToken ct)
    {
        ValidateFile(fileName, bytes.Length);

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
                new { roundId, candidates = candidates.Count });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not BusinessRuleException and not NotFoundException)
        {
            _logger.LogError(ex, "Falha ao processar OCR para a rodada {RoundId}", roundId);
            batch.Status = OcrBatchStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException("ocr.processFailed");
        }

        return await GetBatchAsync(batch.Id, ct);
    }

    public async Task<OcrBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct)
    {
        await EnsureBatchInGroupAsync(batchId, ct);
        var batch = await _db.OcrImportBatches
            .Include(b => b.Candidates)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        return Map(batch);
    }

    public async Task<OcrBatchDto> UpdateCandidateAsync(
        Guid batchId, Guid candidateId, UpdateOcrCandidateRequest request, Guid adminId, CancellationToken ct)
    {
        await EnsureBatchInGroupAsync(batchId, ct);
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
        candidate.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetBatchAsync(batchId, ct);
    }

    public async Task CancelAsync(Guid batchId, Guid adminId, CancellationToken ct)
    {
        await EnsureBatchInGroupAsync(batchId, ct);
        var batch = await _db.OcrImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        batch.Status = OcrBatchStatus.Failed;
        _audit.Add(adminId, "OcrImportCancelled", nameof(OcrImportBatch), batch.Id.ToString(), null);
        await _db.SaveChangesAsync(ct);
    }

    private static string NormalizeLanguage(string? language) => language switch
    {
        "eng" => "eng",
        "por+eng" => "por+eng",
        "eng+por" => "por+eng",
        _ => "por",
    };

    private static OcrBatchDto Map(OcrImportBatch b) => new()
    {
        Id = b.Id,
        RoundId = b.RoundId,
        Status = b.Status,
        LanguageUsed = b.LanguageUsed,
        OriginalFileName = b.OriginalFileName,
        ExtractedText = b.ExtractedText,
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
