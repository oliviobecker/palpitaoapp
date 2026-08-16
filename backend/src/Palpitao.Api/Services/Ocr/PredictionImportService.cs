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

    public PredictionImportService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
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

    public async Task<IReadOnlyDictionary<string, Guid>> GetParticipantAliasesAsync(
        Guid groupId, CancellationToken ct) =>
        await _db.OcrParticipantAliases
            .AsNoTracking()
            .Where(a => a.GroupId == groupId)
            .ToDictionaryAsync(a => a.Alias, a => a.UserId, ct);

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

        var learned = await LearnParticipantAliasesAsync(batch, groupId, adminId, now, ct);

        _audit.Add(adminId, "OcrImportConfirmed", nameof(OcrImportBatch), batch.Id.ToString(),
            new { batch.RoundId, count = batch.Candidates.Count, aliases = learned });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Records the names this batch had to be corrected on. The admin has just told us who
    /// "Paraguaio" is by filing the rows against them; remembering it is the difference between
    /// fixing the same screenshot format once and fixing it every round.
    ///
    /// Only names the matcher could not already resolve on its own are stored, and only when
    /// every row bearing that name agrees on the participant — one image can carry two people,
    /// and a name that pointed at both would be a coin flip on the next import.
    /// </summary>
    private async Task<int> LearnParticipantAliasesAsync(
        OcrImportBatch batch, Guid groupId, Guid adminId, DateTime now, CancellationToken ct)
    {
        var participants = await GroupQueries.ActiveParticipants(_db, groupId).ToListAsync(ct);

        var unanimous = batch.Candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.ParticipantNameRaw) && c.UserId is not null)
            .GroupBy(c => OcrTeamMatcher.NormalizeAlias(c.ParticipantNameRaw!), StringComparer.Ordinal)
            .Where(g => g.Select(c => c.UserId!.Value).Distinct().Count() == 1)
            .Select(g => (Key: g.Key, UserId: g.First().UserId!.Value, Raw: g.First().ParticipantNameRaw!))
            .Where(x => x.Key.Length > 0
                && OcrTeamMatcher.ResolveParticipant(x.Raw, participants) != x.UserId)
            .ToList();

        if (unanimous.Count == 0)
        {
            return 0;
        }

        var keys = unanimous.Select(x => x.Key).ToList();
        var stored = await _db.OcrParticipantAliases
            .Where(a => a.GroupId == groupId && keys.Contains(a.Alias))
            .ToDictionaryAsync(a => a.Alias, ct);

        foreach (var (key, userId, raw) in unanimous)
        {
            if (stored.TryGetValue(key, out var existing))
            {
                // The latest correction wins: an alias learned from junk that turns out to
                // belong to somebody else is re-pointed rather than left wrong forever.
                existing.UserId = userId;
                existing.AliasRaw = raw;
                existing.UpdatedAt = now;
                continue;
            }

            _db.OcrParticipantAliases.Add(new OcrParticipantAlias
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = userId,
                Alias = key,
                AliasRaw = raw,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        _audit.Add(adminId, "OcrParticipantAliasesLearned", nameof(OcrImportBatch), batch.Id.ToString(),
            new { aliases = unanimous.Select(x => new { x.Raw, x.UserId }).ToList() });

        return unanimous.Count;
    }
}
