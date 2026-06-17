using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;

namespace Palpitao.Api.Services.Standings;

public class TemporaryStandingsService : ITemporaryStandingsService
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoring;
    private readonly ICurrentGroupService _current;

    public TemporaryStandingsService(AppDbContext db, IScoringService scoring, ICurrentGroupService current)
    {
        _db = db;
        _scoring = scoring;
        _current = current;
    }

    public async Task<TemporaryStandingsDto> GetTemporaryStandingsAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        // Matches that contribute now: in-progress or finished, with a score.
        var computable = round.Matches
            .Where(m => (m.Status == MatchStatus.InProgress || m.Status == MatchStatus.Finished)
                && m.HomeScore is not null && m.AwayScore is not null)
            .ToList();

        var dismissed = round.Matches.Count(m => m.Status is MatchStatus.Postponed or MatchStatus.Cancelled);
        var computedMatches = computable.Count;
        var remainingMatches = Math.Max(0, round.Matches.Count - computedMatches - dismissed);

        var dto = new TemporaryStandingsDto
        {
            RoundId = round.Id,
            IsTemporary = true,
            RoundStatus = round.Status,
            LastUpdatedAt = round.ResultsUpdatedAt
                ?? round.Matches.Where(m => m.LastResultUpdatedAt is not null).Max(m => (DateTime?)m.LastResultUpdatedAt),
            ComputedMatches = computedMatches,
            RemainingMatches = remainingMatches,
        };

        if (computable.Count == 0)
        {
            return dto;
        }

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId)
            .ToListAsync(ct);

        var participantIds = predictions.Select(p => p.UserId).Distinct().ToList();
        // Active, non-eliminated participants of this round's group (per-group flags).
        var participants = await GroupQueries.ActiveParticipants(_db, round.GroupId)
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(ct);

        var officialTotals = await _db.Standings
            .Where(s => s.SeasonId == round.SeasonId)
            .ToDictionaryAsync(s => s.UserId, s => s.TotalPoints, ct);

        var rows = new List<TemporaryStandingDto>();
        foreach (var participant in participants)
        {
            var userPredictions = predictions
                .Where(p => p.UserId == participant.Id)
                .ToDictionary(p => p.RoundMatchId);

            var points = 0;
            foreach (var match in computable)
            {
                if (!userPredictions.TryGetValue(match.Id, out var prediction))
                {
                    continue;
                }

                var category = _scoring.GetCategory(
                    prediction.PredictedHomeScore, prediction.PredictedAwayScore,
                    match.HomeScore!.Value, match.AwayScore!.Value);
                var basePoints = _scoring.GetBasePoints(category);
                // Same multiplier rule as the official scoring (manual override wins).
                var multiplier = match.ManualMultiplierOverride ?? _scoring.GetMultiplier(
                    match.Competition, match.Phase,
                    match.HomeTeam!.IsBigSevenClub, match.AwayTeam!.IsBigSevenClub,
                    match.HomeTeam!.IsWorldChampion, match.AwayTeam!.IsWorldChampion);
                points += basePoints * multiplier;
            }

            var official = officialTotals.TryGetValue(participant.Id, out var total) ? total : 0;
            rows.Add(new TemporaryStandingDto
            {
                UserId = participant.Id,
                Name = participant.Name,
                RoundTemporaryPoints = points,
                CurrentOfficialTotalPoints = official,
                ProjectedTotalPoints = official + points,
                ComputedMatches = computedMatches,
                RemainingMatches = remainingMatches,
            });
        }

        var position = 1;
        foreach (var row in rows
            .OrderByDescending(r => r.RoundTemporaryPoints)
            .ThenByDescending(r => r.ProjectedTotalPoints)
            .ThenBy(r => r.Name))
        {
            row.Position = position++;
            dto.Standings.Add(row);
        }

        return dto;
    }
}
