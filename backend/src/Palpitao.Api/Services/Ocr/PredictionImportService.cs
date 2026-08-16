using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Orchestrates OCR import: parses text via <see cref="OcrTextParser"/>, matches
/// participants/matches via <see cref="OcrTeamMatcher"/>, and persists a confirmed
/// batch. The parsing and matching logic itself lives in those two helpers.
/// </summary>
public class PredictionImportService : IPredictionImportService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly IOcrAliasService _aliases;

    public PredictionImportService(
        AppDbContext db, IAuditService audit, ICurrentGroupService current, IOcrAliasService aliases)
    {
        _db = db;
        _audit = audit;
        _current = current;
        _aliases = aliases;
    }

    public IReadOnlyList<ParsedPrediction> Parse(string text) => OcrTextParser.Parse(text);

    public IReadOnlyList<OcrPredictionCandidate> BuildCandidates(
        Guid batchId,
        Guid roundId,
        string text,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyList<User> participants,
        IReadOnlyDictionary<string, Guid>? participantAliases = null)
    {
        var now = DateTime.UtcNow;
        var candidates = new List<OcrPredictionCandidate>();

        foreach (var parsed in OcrTextParser.Parse(text))
        {
            var userId = OcrTeamMatcher.ResolveParticipant(parsed.ParticipantName, participants, participantAliases);
            var roundMatchId = OcrTeamMatcher.ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, matches);

            var confidence = (userId is not null ? 0.5 : 0.0) + (roundMatchId is not null ? 0.5 : 0.0);
            var needsReview = userId is null || roundMatchId is null;

            candidates.Add(new OcrPredictionCandidate
            {
                Id = Guid.NewGuid(),
                OcrImportBatchId = batchId,
                RoundId = roundId,
                UserId = userId,
                // Truncated, not trusted: these carry raw OCR output, and a noisy screenshot
                // produces lines longer than any real fixture. An over-long value fails the
                // insert inside the import's try, and the catch that records the failure saves
                // the very same tracked entities — so it fails again and the admin gets an
                // opaque 500 instead of "could not read this image".
                ParticipantNameRaw = Clamp(parsed.ParticipantName, OcrPredictionCandidate.MaxParticipantNameLength),
                RoundMatchId = roundMatchId,
                MatchTextRaw = Clamp(parsed.MatchText, OcrPredictionCandidate.MaxMatchTextLength),
                PredictedHomeScore = parsed.HomeScore,
                PredictedAwayScore = parsed.AwayScore,
                Confidence = confidence,
                NeedsReview = needsReview,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        return candidates;
    }

    private static string? Clamp(string? value, int max) =>
        value is not null && value.Length > max ? value[..max] : value;

    public async Task ConfirmAsync(Guid batchId, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var batch = await _db.OcrImportBatches
            .Include(b => b.Candidates)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.Round!.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        if (batch.Status == OcrBatchStatus.Confirmed)
        {
            throw new BusinessRuleException("ocr.batchAlreadyConfirmed");
        }

        if (batch.Status is not (OcrBatchStatus.Processed or OcrBatchStatus.Reviewed))
        {
            throw new BusinessRuleException("ocr.batchNotReviewable");
        }

        var incomplete = batch.Candidates.Any(c =>
            c.UserId is null || c.RoundMatchId is null ||
            c.PredictedHomeScore is null || c.PredictedAwayScore is null ||
            c.PredictedHomeScore < 0 || c.PredictedAwayScore < 0);

        if (batch.Candidates.Count == 0 || incomplete)
        {
            throw new BusinessRuleException("ocr.incompleteCandidates");
        }

        // Two candidates for the same participant+match would silently race on the same
        // Prediction row below (the second DB lookup cannot see the first pending insert).
        var hasDuplicates = batch.Candidates
            .GroupBy(c => (c.UserId, c.RoundMatchId))
            .Any(g => g.Count() > 1);

        if (hasDuplicates)
        {
            throw new BusinessRuleException("ocr.duplicateCandidates");
        }

        var now = DateTime.UtcNow;
        foreach (var c in batch.Candidates)
        {
            var existing = await _db.Predictions
                .FirstOrDefaultAsync(p => p.RoundMatchId == c.RoundMatchId && p.UserId == c.UserId, ct);

            if (existing is null)
            {
                _db.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    RoundId = batch.RoundId,
                    RoundMatchId = c.RoundMatchId!.Value,
                    UserId = c.UserId!.Value,
                    PredictedHomeScore = c.PredictedHomeScore!.Value,
                    PredictedAwayScore = c.PredictedAwayScore!.Value,
                    SubmittedAt = now,
                    Source = PredictionSource.AdminOcr,
                    CreatedByUserId = adminId,
                });
            }
            else
            {
                existing.PredictedHomeScore = c.PredictedHomeScore!.Value;
                existing.PredictedAwayScore = c.PredictedAwayScore!.Value;
                existing.UpdatedAt = now;
                existing.Source = PredictionSource.AdminOcr;
                existing.UpdatedByUserId = adminId;
            }
        }

        batch.Status = OcrBatchStatus.Confirmed;
        batch.ConfirmedAt = now;

        // Enqueued on this unit of work, saved by the SaveChanges below — the aliases and the
        // predictions they describe land together or not at all.
        var learned = await _aliases.LearnAsync(batch.Candidates, groupId, adminId, now, ct);

        _audit.Add(adminId, "OcrImportConfirmed", nameof(OcrImportBatch), batch.Id.ToString(),
            new { batch.RoundId, count = batch.Candidates.Count, aliases = learned });

        await _db.SaveChangesAsync(ct);
    }

}
