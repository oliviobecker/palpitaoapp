using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Standings;

public class StandingsService : IStandingsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentGroupService _current;

    public StandingsService(AppDbContext db, ICurrentGroupService current)
    {
        _db = db;
        _current = current;
    }

    public async Task RecomputeSeasonStandingsAsync(Guid seasonId, CancellationToken ct)
    {
        // Derive the owning group from the season so inserted standings carry the
        // correct tenant (rather than relying on the default-group column default).
        var groupId = await _db.Seasons
            .Where(s => s.Id == seasonId)
            .Select(s => s.GroupId)
            .FirstAsync(ct);

        // Aggregate per participant and round number in the database, then per participant in
        // memory. The parts of a round played in parts ("10.1", "10.2") share its number and
        // count as one round: played if the participant is not absent in it, one absence if
        // they are (only the last part records it). Conditional sums translate to SQL CASE
        // expressions; the rows scale with participants × round numbers, not × results.
        var perRound = await _db.RoundParticipantResults
            .AsNoTracking()
            .Where(r => r.SeasonId == seasonId)
            .Join(_db.Rounds, r => r.RoundId, round => round.Id, (r, round) => new
            {
                r.UserId,
                round.Number,
                r.FinalPoints,
                r.PenaltyPoints,
                r.WasAbsent,
            })
            .GroupBy(x => new { x.UserId, x.Number })
            .Select(g => new
            {
                g.Key.UserId,
                FinalPoints = g.Sum(x => x.FinalPoints),
                PenaltyPoints = g.Sum(x => x.PenaltyPoints),
                Absences = g.Sum(x => x.WasAbsent ? 1 : 0),
            })
            .ToListAsync(ct);

        var aggregates = perRound
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                FinalPoints = g.Sum(x => x.FinalPoints),
                PenaltyPoints = g.Sum(x => x.PenaltyPoints),
                PlayedRounds = g.Count(x => x.Absences == 0),
                AbsenceCount = g.Count(x => x.Absences > 0),
            })
            .ToList();

        // Exact-score counts per participant across the season's scored rounds.
        var exactCounts = await _db.PredictionScores
            .AsNoTracking()
            .Where(p => p.IsExactScore
                && _db.Rounds.Any(r => r.Id == p.RoundId && r.SeasonId == seasonId))
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var userIds = aggregates.Select(a => a.UserId).ToList();
        var names = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        // Final ordering is in memory: the name tie-break uses an ordinal-ignore-case
        // comparison that has no faithful SQL translation.
        var rows = aggregates
            .Select(a => new
            {
                a.UserId,
                Name = names.GetValueOrDefault(a.UserId, string.Empty),
                TotalPoints = a.FinalPoints - a.PenaltyPoints,
                a.PlayedRounds,
                a.AbsenceCount,
                a.PenaltyPoints,
                ExactCount = exactCounts.GetValueOrDefault(a.UserId),
            })
            .OrderByDescending(r => r.TotalPoints)
            .ThenBy(r => r.AbsenceCount)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Replace the previous standing rows for the season.
        var previous = await _db.Standings.Where(s => s.SeasonId == seasonId).ToListAsync(ct);
        _db.Standings.RemoveRange(previous);

        var now = DateTime.UtcNow;
        var position = 1;
        foreach (var row in rows)
        {
            _db.Standings.Add(new Standing
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SeasonId = seasonId,
                UserId = row.UserId,
                TotalPoints = row.TotalPoints,
                Position = position++,
                PlayedRounds = row.PlayedRounds,
                ExactCount = row.ExactCount,
                AbsenceCount = row.AbsenceCount,
                PenaltyPoints = row.PenaltyPoints,
                UpdatedAt = now,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StandingDto>> GetStandingsAsync(Guid seasonId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var seasonExists = await _db.Seasons.AnyAsync(s => s.Id == seasonId && s.GroupId == groupId, ct);
        if (!seasonExists)
        {
            throw new NotFoundException("notFound.season");
        }

        // Eliminated members of this group, loaded once into a set rather than a
        // correlated subquery evaluated per standing row.
        var eliminated = (await _db.GroupUsers
            .AsNoTracking()
            .Where(gu => gu.GroupId == groupId && gu.IsEliminated)
            .Select(gu => gu.UserId)
            .ToListAsync(ct)).ToHashSet();

        var rows = await _db.Standings
            .AsNoTracking()
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.Position)
            .Join(_db.Users, s => s.UserId, u => u.Id, (s, u) => new StandingDto
            {
                Position = s.Position,
                UserId = s.UserId,
                Name = u.Name,
                TotalPoints = s.TotalPoints,
                PlayedRounds = s.PlayedRounds,
                AbsenceCount = s.AbsenceCount,
                PenaltyPoints = s.PenaltyPoints,
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            // Elimination is per-group (on the membership of this season's group).
            row.IsEliminated = eliminated.Contains(row.UserId);
        }

        return rows;
    }
}
