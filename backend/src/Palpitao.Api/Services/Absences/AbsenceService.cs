using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;
using Sentry;

namespace Palpitao.Api.Services.Absences;

public class AbsenceService : IAbsenceService
{
    /// <summary>
    /// First absence ordinal that costs points. Fixed by the domain rules (the 1st and 2nd
    /// absences only zero the round); what the admin configures is how much it costs and
    /// which ordinal eliminates.
    /// </summary>
    private const int FirstPenalizedAbsence = 3;

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly ISeasonScoringConfigService _config;

    public AbsenceService(
        AppDbContext db, IAuditService audit, ICurrentGroupService current, ISeasonScoringConfigService config)
    {
        _db = db;
        _audit = audit;
        _current = current;
        _config = config;
    }

    public async Task<bool> IsAbsentAsync(Guid roundId, Guid userId, CancellationToken ct)
    {
        var absentees = await DetectAbsenteesAsync(roundId, ct);
        return absentees.Contains(userId);
    }

    public async Task<IReadOnlyList<Guid>> DetectAbsenteesAsync(Guid roundId, CancellationToken ct)
    {
        var round = await _db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw new NotFoundException("notFound.round");

        var matchCount = round.Matches.Count;

        // Roster = the round's group's approved participants (never global users).
        var participants = await GroupQueries.ActiveParticipants(_db, round.GroupId)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var overrides = await _db.AbsenceOverrides
            .Where(o => o.RoundId == roundId)
            .ToDictionaryAsync(o => o.UserId, o => o.IsAbsent, ct);

        var predictionCounts = await _db.Predictions
            .Where(p => p.RoundId == roundId)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var absentees = new List<Guid>();
        foreach (var userId in participants)
        {
            bool absent;
            if (overrides.TryGetValue(userId, out var forced))
            {
                absent = forced;
            }
            else
            {
                predictionCounts.TryGetValue(userId, out var count);
                absent = count < matchCount;
            }

            if (absent)
            {
                absentees.Add(userId);
            }
        }

        return absentees;
    }

    public async Task<int> CountSeasonAbsencesAsync(Guid seasonId, Guid userId, CancellationToken ct)
    {
        return await _db.Absences
            .CountAsync(a => a.UserId == userId
                && _db.Rounds.Any(r => r.Id == a.RoundId && r.SeasonId == seasonId), ct);
    }

    public async Task<IReadOnlyList<AbsenceOutcome>> ProcessRoundAbsencesAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
    {
        var round = await _db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw new NotFoundException("notFound.round");

        // Idempotency: drop this round's previous absence records before recomputing.
        var previous = await _db.Absences.Where(a => a.RoundId == roundId).ToListAsync(ct);
        if (previous.Count > 0)
        {
            _db.Absences.RemoveRange(previous);
            await _db.SaveChangesAsync(ct);
        }

        var absentees = await DetectAbsenteesAsync(roundId, ct);
        var now = DateTime.UtcNow;
        var outcomes = new List<AbsenceOutcome>();

        // Rounds before the season's AbsenceFromRound still zero the absentee's round —
        // they just don't climb the punishment ladder (no Absence row, so no ordinal, no
        // penalty, no elimination). The delete above and the result upsert below run
        // regardless, so raising the threshold and re-scoring cleans up stale rows and
        // still leaves every absentee with their zeroed result row.
        var rules = await _config.GetRuleParamsAsync(round.SeasonId, ct);
        var counted = round.Number >= rules.AbsenceFromRound;

        // Prior season absences for every absentee in a single query (this round's
        // records were cleared above), instead of one COUNT query per absentee.
        var priorCounts = await _db.Absences
            .Where(a => absentees.Contains(a.UserId)
                && _db.Rounds.Any(r => r.Id == a.RoundId && r.SeasonId == round.SeasonId))
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        foreach (var userId in absentees)
        {
            var absenceNumber = counted ? priorCounts.GetValueOrDefault(userId) + 1 : 0;
            var (penalty, eliminated) = counted
                ? PenaltyFor(absenceNumber, rules.AbsencePenaltyPoints, rules.AbsenceEliminationCount)
                : (0, false);

            if (counted)
            {
                _db.Absences.Add(new Absence
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    UserId = userId,
                    AbsenceNumber = absenceNumber,
                    PenaltyPoints = penalty,
                    CreatedAt = now,
                });
            }

            if (eliminated)
            {
                var membership = await _db.GroupUsers
                    .FirstAsync(gu => gu.GroupId == round.GroupId && gu.UserId == userId, ct);
                membership.IsEliminated = true;
                SentrySdk.AddBreadcrumb("User eliminated by absence.", "absences", data: new Dictionary<string, string>
                {
                    ["roundId"] = roundId.ToString(),
                    ["userId"] = userId.ToString(),
                    ["absenceNumber"] = absenceNumber.ToString(),
                });
            }

            await UpsertAbsentResultAsync(round, userId, penalty, eliminated, now, ct);

            outcomes.Add(new AbsenceOutcome(userId, absenceNumber, penalty, eliminated));
        }

        _audit.Add(actingUserId, "RoundAbsencesProcessed", nameof(Round), roundId.ToString(),
            new { absent = outcomes.Count });
        await _db.SaveChangesAsync(ct);

        return outcomes;
    }

    public async Task ApplyOverrideAsync(Guid roundId, AbsenceOverrideRequest request, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        await StageOverrideAsync(groupId, roundId, request, actingUserId, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Upsert + audit staged on the shared DbContext, without saving, so a caller can commit
    /// it together with its own changes in a single transaction.
    /// </summary>
    private async Task StageOverrideAsync(
        Guid groupId, Guid roundId, AbsenceOverrideRequest request, Guid actingUserId, CancellationToken ct)
    {
        var roundExists = await _db.Rounds.AnyAsync(r => r.Id == roundId && r.GroupId == groupId, ct);
        if (!roundExists)
        {
            throw new NotFoundException("notFound.round");
        }

        var userExists = await GroupQueries.AllParticipants(_db, groupId).AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists)
        {
            throw new NotFoundException("notFound.participant");
        }

        var existing = await _db.AbsenceOverrides
            .FirstOrDefaultAsync(o => o.RoundId == roundId && o.UserId == request.UserId, ct);

        if (existing is null)
        {
            _db.AbsenceOverrides.Add(new AbsenceOverride
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                UserId = request.UserId,
                IsAbsent = request.IsAbsent,
                Justification = request.Justification,
                CreatedByUserId = actingUserId,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.IsAbsent = request.IsAbsent;
            existing.Justification = request.Justification;
            existing.CreatedByUserId = actingUserId;
            existing.CreatedAt = DateTime.UtcNow;
        }

        _audit.Add(actingUserId, "AbsenceOverride", nameof(Round), roundId.ToString(),
            new { request.UserId, request.IsAbsent, request.Justification });
    }

    public async Task<IReadOnlyList<AbsenceCandidateRoundDto>> GetAbsenceCandidateRoundsAsync(
        Guid userId, CancellationToken ct)
        => await LoadCandidatesAsync(await _current.GetGroupIdAsync(ct), userId, ct);

    /// <summary>
    /// Rounds the participant can be recorded absent for. Takes the group id so callers that
    /// already resolved it (and are mid-transaction) do not resolve it twice.
    /// </summary>
    private async Task<List<AbsenceCandidateRoundDto>> LoadCandidatesAsync(
        Guid groupId, Guid userId, CancellationToken ct)
    {
        // AllParticipants, not ActiveParticipants: the participant is inactive or eliminated
        // by definition here -- that is exactly why the admin is (re)activating them.
        var isMember = await GroupQueries.AllParticipants(_db, groupId).AnyAsync(u => u.Id == userId, ct);
        if (!isMember)
        {
            throw new NotFoundException("notFound.participant");
        }

        var seasonId = await _db.Seasons
            .Where(s => s.IsActive && s.GroupId == groupId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (seasonId is null)
        {
            return [];
        }

        // Locked/Scored only: a Published round still accepts predictions (the participant
        // can simply submit them), and Draft/Cancelled rounds never score.
        var rounds = await _db.Rounds
            .Where(r => r.SeasonId == seasonId
                && (r.Status == RoundStatus.Locked || r.Status == RoundStatus.Scored))
            .OrderBy(r => r.Number)
            .Select(r => new
            {
                r.Id,
                r.Number,
                r.Title,
                r.Status,
                MatchCount = r.Matches.Count,
                PredictionCount = _db.Predictions.Count(p => p.RoundId == r.Id && p.UserId == userId),
                ForcedAbsent = _db.AbsenceOverrides
                    .Where(o => o.RoundId == r.Id && o.UserId == userId)
                    .Select(o => (bool?)o.IsAbsent)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        // A round with no matches drops out on its own (0 < 0 is false).
        return rounds
            .Where(r => r.PredictionCount < r.MatchCount && r.ForcedAbsent != true)
            .Select(r => new AbsenceCandidateRoundDto
            {
                RoundId = r.Id,
                Number = r.Number,
                Title = r.Title,
                Status = r.Status,
                MatchCount = r.MatchCount,
                PredictionCount = r.PredictionCount,
                // An override currently marking them present is surfaced rather than hidden:
                // confirming replaces it, and hiding the conflict would be worse.
                HasPresentOverride = r.ForcedAbsent == false,
            })
            .ToList();
    }

    public async Task StageAbsenceOverridesAsync(
        Guid userId, IReadOnlyCollection<Guid> roundIds, string justification, Guid actingUserId, CancellationToken ct)
        => await StageAbsenceOverridesCoreAsync(
            await _current.GetGroupIdAsync(ct), userId, roundIds, justification, actingUserId, ct);

    private async Task StageAbsenceOverridesCoreAsync(
        Guid groupId, Guid userId, IReadOnlyCollection<Guid> roundIds, string justification,
        Guid actingUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new BusinessRuleException("common.justificationRequired");
        }

        // Distinct is required: two identical ids would each miss the other's unsaved insert
        // and violate the unique (RoundId, UserId) index at SaveChanges.
        var requested = roundIds.Distinct().ToList();
        if (requested.Count == 0)
        {
            return;
        }

        // Already forced absent -> nothing to stage, so a retry or a double submit is a no-op.
        var alreadyAbsent = await _db.AbsenceOverrides
            .Where(o => o.UserId == userId && o.IsAbsent && requested.Contains(o.RoundId))
            .Select(o => o.RoundId)
            .ToListAsync(ct);

        var candidates = (await LoadCandidatesAsync(groupId, userId, ct))
            .Select(c => c.RoundId)
            .ToHashSet();

        foreach (var roundId in requested.Except(alreadyAbsent))
        {
            // Not a candidate: another tenant's round, one not closed for predictions, one
            // outside the active season, or predictions that arrived in the meantime. The
            // generic message leaks nothing about the other cases.
            if (!candidates.Contains(roundId))
            {
                throw new BusinessRuleException("absence.roundNotEligible");
            }

            await StageOverrideAsync(groupId, roundId, new AbsenceOverrideRequest
            {
                UserId = userId,
                IsAbsent = true,
                Justification = justification,
            }, actingUserId, ct);
        }
    }

    public async Task ReactivateAsync(
        Guid userId, string justification, IReadOnlyCollection<Guid>? absentRoundIds,
        Guid actingUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new BusinessRuleException("common.justificationRequired");
        }

        var groupId = await _current.GetGroupIdAsync(ct);
        var membership = await GroupQueries.ApprovedMemberships(_db, groupId)
            .FirstOrDefaultAsync(gu => gu.UserId == userId, ct)
            ?? throw new NotFoundException("notFound.participant");

        membership.IsEliminated = false;
        membership.IsActive = true;

        // Safe before the save: candidate resolution reads rounds/predictions/overrides,
        // never the membership flags, so there is no read-your-own-writes hazard.
        if (absentRoundIds is { Count: > 0 })
        {
            await StageAbsenceOverridesCoreAsync(
                groupId, userId, absentRoundIds, justification, actingUserId, ct);
        }

        _audit.Add(actingUserId, "ParticipantReactivated", nameof(User), userId.ToString(),
            new { justification, absentRounds = absentRoundIds?.Count ?? 0 });

        // One commit: membership flags + override rows + every audit row.
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AbsenceDto>> GetUserAbsencesAsync(Guid userId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Absences
            .Where(a => a.UserId == userId)
            .Join(_db.Rounds.Where(r => r.GroupId == groupId), a => a.RoundId, r => r.Id, (a, r) => new AbsenceDto
            {
                RoundId = a.RoundId,
                RoundNumber = r.Number,
                UserId = a.UserId,
                AbsenceNumber = a.AbsenceNumber,
                PenaltyPoints = a.PenaltyPoints,
                CreatedAt = a.CreatedAt,
            })
            .OrderBy(a => a.RoundNumber)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AbsenceDto>> GetRoundAbsencesAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Absences
            .Where(a => a.RoundId == roundId)
            .Join(_db.Rounds.Where(r => r.GroupId == groupId), a => a.RoundId, r => r.Id, (a, r) => new AbsenceDto
            {
                RoundId = a.RoundId,
                RoundNumber = r.Number,
                UserId = a.UserId,
                AbsenceNumber = a.AbsenceNumber,
                PenaltyPoints = a.PenaltyPoints,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Maps an absence ordinal to its (penalty, eliminated) outcome under the season's
    /// rules. Defaults reproduce the classic table: 1st–2nd nothing, 3rd–4th −20 points,
    /// 5th eliminates. Elimination is checked first, so lowering
    /// <paramref name="eliminationCount"/> to 2 or less legitimately removes the penalty
    /// band instead of both applying.
    /// </summary>
    private static (int Penalty, bool Eliminated) PenaltyFor(
        int absenceNumber, int penaltyPoints, int eliminationCount)
    {
        if (absenceNumber >= eliminationCount)
        {
            return (0, true);
        }

        return absenceNumber >= FirstPenalizedAbsence ? (penaltyPoints, false) : (0, false);
    }

    private async Task UpsertAbsentResultAsync(
        Round round, Guid userId, int penalty, bool eliminated, DateTime now, CancellationToken ct)
    {
        var result = await _db.RoundParticipantResults
            .FirstOrDefaultAsync(r => r.RoundId == round.Id && r.UserId == userId, ct);

        if (result is null)
        {
            _db.RoundParticipantResults.Add(new RoundParticipantResult
            {
                Id = Guid.NewGuid(),
                GroupId = round.GroupId,
                SeasonId = round.SeasonId,
                RoundId = round.Id,
                UserId = userId,
                GrossPoints = 0,
                FinalPoints = 0,
                PenaltyPoints = penalty,
                WasAbsent = true,
                WasEliminated = eliminated,
                FlavioRuleApplied = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            result.GrossPoints = 0;
            result.FinalPoints = 0;
            result.PenaltyPoints = penalty;
            result.WasAbsent = true;
            result.WasEliminated = eliminated;
            result.UpdatedAt = now;
        }
    }
}
