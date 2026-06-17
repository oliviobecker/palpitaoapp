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

        var results = await _db.RoundParticipantResults
            .Where(r => r.SeasonId == seasonId)
            .ToListAsync(ct);

        // Exact-score counts per participant (across the season's scored rounds).
        var roundIds = await _db.Rounds.Where(r => r.SeasonId == seasonId).Select(r => r.Id).ToListAsync(ct);
        var exactCounts = await _db.PredictionScores
            .Where(p => roundIds.Contains(p.RoundId) && p.IsExactScore)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var userIds = results.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var rows = results
            .GroupBy(r => r.UserId)
            .Select(g =>
            {
                var name = users.TryGetValue(g.Key, out var u) ? u.Name : string.Empty;
                var penalty = g.Sum(r => r.PenaltyPoints);
                return new
                {
                    UserId = g.Key,
                    Name = name,
                    TotalPoints = g.Sum(r => r.FinalPoints) - penalty,
                    PlayedRounds = g.Count(r => !r.WasAbsent),
                    AbsenceCount = g.Count(r => r.WasAbsent),
                    PenaltyPoints = penalty,
                    ExactCount = exactCounts.TryGetValue(g.Key, out var c) ? c : 0,
                };
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

        return await _db.Standings
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
                // Elimination is per-group (on the membership of this season's group).
                IsEliminated = _db.GroupUsers.Any(gu => gu.GroupId == groupId && gu.UserId == s.UserId && gu.IsEliminated),
            })
            .ToListAsync(ct);
    }
}
