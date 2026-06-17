using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Scouts;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Scouts;

public class ScoutService : IScoutService
{
    private readonly AppDbContext _db;
    private readonly ICurrentGroupService _current;

    public ScoutService(AppDbContext db, ICurrentGroupService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<RoundScoutDto> GetRoundScoutAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        // Every participant's prediction for this round, with the participant name.
        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.User!.Role == UserRole.Participant)
            .Select(p => new
            {
                p.RoundMatchId,
                p.PredictedHomeScore,
                p.PredictedAwayScore,
                Name = p.User!.Name,
            })
            .ToListAsync(ct);

        var byMatch = predictions
            .GroupBy(p => p.RoundMatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dto = new RoundScoutDto
        {
            RoundId = round.Id,
            RoundNumber = round.Number,
            RoundTitle = round.Title,
        };

        foreach (var match in round.Matches.OrderBy(m => m.Order).ThenBy(m => m.StartsAt))
        {
            var matchDto = new ScoutMatchDto
            {
                RoundMatchId = match.Id,
                HomeTeamName = match.HomeTeam?.Name ?? string.Empty,
                AwayTeamName = match.AwayTeam?.Name ?? string.Empty,
            };

            if (byMatch.TryGetValue(match.Id, out var matchPredictions))
            {
                matchDto.Groups = matchPredictions
                    .GroupBy(p => (p.PredictedHomeScore, p.PredictedAwayScore))
                    .Select(g => new ScoutScoreGroupDto
                    {
                        HomeScore = g.Key.PredictedHomeScore,
                        AwayScore = g.Key.PredictedAwayScore,
                        Names = g.Select(p => p.Name)
                            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                            .ToList(),
                    })
                    .OrderBy(g => g.HomeScore)
                    .ThenBy(g => g.AwayScore)
                    .ToList();
            }

            dto.Matches.Add(matchDto);
        }

        return dto;
    }
}
