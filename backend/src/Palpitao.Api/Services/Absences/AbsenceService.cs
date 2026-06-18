using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Sentry;

namespace Palpitao.Api.Services.Absences;

public class AbsenceService : IAbsenceService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public AbsenceService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
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
            var absenceNumber = priorCounts.GetValueOrDefault(userId) + 1;
            var (penalty, eliminated) = PenaltyFor(absenceNumber);

            _db.Absences.Add(new Absence
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                UserId = userId,
                AbsenceNumber = absenceNumber,
                PenaltyPoints = penalty,
                CreatedAt = now,
            });

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
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid userId, string justification, Guid actingUserId, CancellationToken ct)
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

        _audit.Add(actingUserId, "ParticipantReactivated", nameof(User), userId.ToString(),
            new { justification });
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

    /// <summary>Maps an absence ordinal to its (penalty, eliminated) outcome.</summary>
    private static (int Penalty, bool Eliminated) PenaltyFor(int absenceNumber) => absenceNumber switch
    {
        3 or 4 => (20, false),
        >= 5 => (0, true),
        _ => (0, false),
    };

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
