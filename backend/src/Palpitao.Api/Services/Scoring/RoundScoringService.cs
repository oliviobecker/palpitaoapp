using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Standings;
using Sentry;

namespace Palpitao.Api.Services.Scoring;

public class RoundScoringService : IRoundScoringService
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoring;
    private readonly IAbsenceService _absences;
    private readonly IFlavioRuleService _flavio;
    private readonly IStandingsService _standings;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public RoundScoringService(
        AppDbContext db,
        IScoringService scoring,
        IAbsenceService absences,
        IFlavioRuleService flavio,
        IStandingsService standings,
        IAuditService audit,
        ICurrentGroupService current)
    {
        _db = db;
        _scoring = scoring;
        _absences = absences;
        _flavio = flavio;
        _standings = standings;
        _audit = audit;
        _current = current;
    }

    public async Task SetMatchResultAsync(Guid matchId, MatchResultRequest request, Guid actingUserId, CancellationToken ct)
    {
        if (request.HomeScore < 0 || request.AwayScore < 0)
        {
            throw new BusinessRuleException("prediction.negativeScore");
        }

        var groupId = await _current.GetGroupIdAsync(ct);
        var match = await _db.RoundMatches
            .Include(m => m.Round)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.Round!.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.match");

        if (match.Round!.Status is RoundStatus.Draft or RoundStatus.Cancelled)
        {
            throw new BusinessRuleException("result.cannotRegister");
        }

        match.HomeScore = request.HomeScore;
        match.AwayScore = request.AwayScore;
        match.IsFinished = true;
        // A manually entered result counts as finished for the temporary standings.
        match.Status = MatchStatus.Finished;
        match.ResultSource = "Manual";
        match.LastResultUpdatedAt = DateTime.UtcNow;

        _audit.Add(actingUserId, "MatchResultEntered", nameof(RoundMatch), match.Id.ToString(),
            new { request.HomeScore, request.AwayScore });
        await _db.SaveChangesAsync(ct);
    }

    public Task<RoundResultsDto> ScoreRoundAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
        => ScoreRoundInternalAsync(roundId, actingUserId, updateStandings: true, ct);

    public async Task RecalculateSeasonAsync(Guid seasonId, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var seasonExists = await _db.Seasons.AnyAsync(s => s.Id == seasonId && s.GroupId == groupId, ct);
        if (!seasonExists)
        {
            throw new NotFoundException("notFound.season");
        }

        var roundIds = await _db.Rounds.Where(r => r.SeasonId == seasonId).Select(r => r.Id).ToListAsync(ct);

        // Reset eliminations so they are re-derived from the recomputed absences
        // (only this group's approved participant memberships, including currently
        // eliminated). Elimination is a per-group flag on GroupUser.
        var memberships = await GroupQueries.ApprovedMemberships(_db, groupId).ToListAsync(ct);
        foreach (var gu in memberships)
        {
            gu.IsEliminated = false;
        }

        // Clear previous calculations for the season.
        _db.PredictionScores.RemoveRange(_db.PredictionScores.Where(p => roundIds.Contains(p.RoundId)));
        _db.RoundParticipantResults.RemoveRange(_db.RoundParticipantResults.Where(r => r.SeasonId == seasonId));
        _db.Absences.RemoveRange(_db.Absences.Where(a => roundIds.Contains(a.RoundId)));
        await _db.SaveChangesAsync(ct);

        // Re-score the already-finished rounds in order.
        var roundsToScore = await _db.Rounds
            .Where(r => r.SeasonId == seasonId && r.Status == RoundStatus.Scored)
            .OrderBy(r => r.Number)
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var id in roundsToScore)
        {
            await ScoreRoundInternalAsync(id, actingUserId, updateStandings: false, ct);
        }

        await _standings.RecomputeSeasonStandingsAsync(seasonId, ct);

        _audit.Add(actingUserId, "SeasonRecalculated", nameof(Season), seasonId.ToString(),
            new { rounds = roundsToScore.Count });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<RoundResultsDto> ScoreRoundInternalAsync(
        Guid roundId, Guid actingUserId, bool updateStandings, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        if (round.Status is not (RoundStatus.Locked or RoundStatus.Scored))
        {
            throw new BusinessRuleException("round.mustBeLockedToScore");
        }

        if (round.Matches.Count == 0)
        {
            throw new BusinessRuleException("round.noMatches");
        }

        if (round.Matches.Any(m => m.HomeScore is null || m.AwayScore is null))
        {
            throw new BusinessRuleException("round.allResultsRequired");
        }

        // Idempotency: drop this round's previous per-match scores and results.
        _db.PredictionScores.RemoveRange(_db.PredictionScores.Where(p => p.RoundId == roundId));
        _db.RoundParticipantResults.RemoveRange(_db.RoundParticipantResults.Where(r => r.RoundId == roundId));
        await _db.SaveChangesAsync(ct);

        var participants = await GroupQueries.ActiveParticipants(_db, round.GroupId)
            .ToListAsync(ct);

        var absentees = (await _absences.DetectAbsenteesAsync(roundId, ct)).ToHashSet();

        // Flávio targets: England penalizes the live leader(s) before the round;
        // the World Cup uses the single target captured at publication.
        var tournamentType = await _db.Groups
            .Where(g => g.Id == round.GroupId)
            .Select(g => g.TournamentType)
            .FirstAsync(ct);
        var flavioTargets = tournamentType == TournamentType.FifaWorldCup
            ? (round.FlavioRuleTargetUserId is Guid wcTarget ? new HashSet<Guid> { wcTarget } : new HashSet<Guid>())
            : (await _flavio.GetLeadersBeforeRoundAsync(round.SeasonId, ct)).ToHashSet();

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var participant in participants.Where(p => !absentees.Contains(p.Id)))
        {
            var userPredictions = predictions
                .Where(p => p.UserId == participant.Id)
                .ToDictionary(p => p.RoundMatchId);

            var gross = 0;
            foreach (var match in round.Matches)
            {
                if (!userPredictions.TryGetValue(match.Id, out var prediction))
                {
                    continue;
                }

                var actualHome = match.HomeScore!.Value;
                var actualAway = match.AwayScore!.Value;

                var category = _scoring.GetCategory(prediction.PredictedHomeScore, prediction.PredictedAwayScore, actualHome, actualAway);
                var basePoints = _scoring.GetBasePoints(category);
                var multiplier = match.ManualMultiplierOverride ?? _scoring.GetMultiplier(
                    match.Competition, match.Phase,
                    match.HomeTeam!.IsBigSevenClub, match.AwayTeam!.IsBigSevenClub,
                    match.HomeTeam!.IsWorldChampion, match.AwayTeam!.IsWorldChampion);
                var finalPoints = basePoints * multiplier;

                _db.PredictionScores.Add(new PredictionScore
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    RoundMatchId = match.Id,
                    UserId = participant.Id,
                    PredictionId = prediction.Id,
                    BasePoints = basePoints,
                    Multiplier = multiplier,
                    FinalPoints = finalPoints,
                    ScoreCategory = category,
                    IsExactScore = _scoring.IsExactScore(prediction.PredictedHomeScore, prediction.PredictedAwayScore, actualHome, actualAway),
                    IsCorrectColumn = _scoring.IsCorrectColumn(prediction.PredictedHomeScore, prediction.PredictedAwayScore, actualHome, actualAway),
                    CreatedAt = now,
                });

                gross += finalPoints;
            }

            var flavioApplied = false;
            var roundFinal = gross;
            if (flavioTargets.Contains(participant.Id) && await _flavio.ShouldPenalizeLeaderAsync(roundId, participant.Id, ct))
            {
                roundFinal = _flavio.ApplyHalfPenalty(gross);
                flavioApplied = true;
                SentrySdk.AddBreadcrumb("Flavio rule applied.", "scoring", data: new Dictionary<string, string>
                {
                    ["roundId"] = roundId.ToString(),
                    ["userId"] = participant.Id.ToString(),
                });
            }

            _db.RoundParticipantResults.Add(new RoundParticipantResult
            {
                Id = Guid.NewGuid(),
                GroupId = round.GroupId,
                SeasonId = round.SeasonId,
                RoundId = roundId,
                UserId = participant.Id,
                GrossPoints = gross,
                FinalPoints = roundFinal,
                PenaltyPoints = 0,
                WasAbsent = false,
                WasEliminated = false,
                FlavioRuleApplied = flavioApplied,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        // Absences: records the absence, applies penalties and eliminations and
        // writes the absent participants' RoundParticipantResult rows.
        await _absences.ProcessRoundAbsencesAsync(roundId, actingUserId, ct);

        round.Status = RoundStatus.Scored;
        _audit.Add(actingUserId, "RoundScored", nameof(Round), roundId.ToString(),
            new { absent = absentees.Count });
        await _db.SaveChangesAsync(ct);
        SentrySdk.AddBreadcrumb("Round scoring persisted.", "scoring", data: new Dictionary<string, string>
        {
            ["roundId"] = roundId.ToString(),
            ["absent"] = absentees.Count.ToString(),
        });

        if (updateStandings)
        {
            await _standings.RecomputeSeasonStandingsAsync(round.SeasonId, ct);
        }

        return await GetRoundResultsAsync(roundId, ct);
    }

    public async Task<RoundResultsDto> GetRoundResultsAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        var results = await _db.RoundParticipantResults
            .Where(r => r.RoundId == roundId)
            .ToListAsync(ct);

        var scores = await _db.PredictionScores
            .Where(p => p.RoundId == roundId)
            .ToListAsync(ct);

        var userIds = results.Select(r => r.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var dto = new RoundResultsDto
        {
            RoundId = round.Id,
            Status = round.Status,
            Matches = round.Matches
                .OrderBy(m => m.Order)
                .ThenBy(m => m.StartsAt)
                .Select(m => new RoundResultMatchDto
                {
                    RoundMatchId = m.Id,
                    Competition = m.Competition,
                    Phase = m.Phase,
                    HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
                    AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
                    HomeScore = m.HomeScore,
                    AwayScore = m.AwayScore,
                    IsFinished = m.IsFinished,
                    Multiplier = m.ManualMultiplierOverride ?? _scoring.GetMultiplier(
                        m.Competition, m.Phase,
                        m.HomeTeam?.IsBigSevenClub ?? false,
                        m.AwayTeam?.IsBigSevenClub ?? false,
                        m.HomeTeam?.IsWorldChampion ?? false,
                        m.AwayTeam?.IsWorldChampion ?? false),
                })
                .ToList(),
        };

        foreach (var result in results.OrderByDescending(r => r.FinalPoints))
        {
            dto.Participants.Add(new RoundResultParticipantDto
            {
                UserId = result.UserId,
                Name = users.TryGetValue(result.UserId, out var name) ? name : string.Empty,
                GrossPoints = result.GrossPoints,
                FinalPoints = result.FinalPoints,
                PenaltyPoints = result.PenaltyPoints,
                WasAbsent = result.WasAbsent,
                WasEliminated = result.WasEliminated,
                FlavioRuleApplied = result.FlavioRuleApplied,
                MatchScores = scores
                    .Where(s => s.UserId == result.UserId)
                    .Select(s => new MatchScoreDto
                    {
                        RoundMatchId = s.RoundMatchId,
                        BasePoints = s.BasePoints,
                        Multiplier = s.Multiplier,
                        FinalPoints = s.FinalPoints,
                        ScoreCategory = s.ScoreCategory,
                        IsExactScore = s.IsExactScore,
                        IsCorrectColumn = s.IsCorrectColumn,
                    })
                    .ToList(),
            });
        }

        return dto;
    }
}
