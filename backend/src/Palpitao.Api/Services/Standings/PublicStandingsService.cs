using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Public;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;

namespace Palpitao.Api.Services.Standings;

/// <inheritdoc />
public class PublicStandingsService : IPublicStandingsService
{
    /// <summary>
    /// The only round states a public link may expose. Predictions are already closed in
    /// both, so showing them cannot help anyone copy a pick.
    /// </summary>
    private static readonly RoundStatus[] VisibleStates = [RoundStatus.Locked, RoundStatus.Scored];

    private readonly AppDbContext _db;
    private readonly IScoringService _scoring;
    private readonly ISeasonScoringConfigService _config;

    public PublicStandingsService(AppDbContext db, IScoringService scoring, ISeasonScoringConfigService config)
    {
        _db = db;
        _scoring = scoring;
        _config = config;
    }

    /// <summary>
    /// Resolves the key to a published season. Every failure — malformed key, unknown key,
    /// publishing switched off — raises the same not-found, so probing cannot tell an
    /// existing-but-unpublished season from one that never existed.
    /// </summary>
    private async Task<Season> ResolveSeasonAsync(string key, CancellationToken ct)
    {
        var normalized = PublicKeyGenerator.Normalize(key);
        if (normalized.Length == 0)
        {
            throw new NotFoundException("notFound.season");
        }

        return await _db.Seasons
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.PublicKey == normalized && s.PublicStandingsEnabled, ct)
            ?? throw new NotFoundException("notFound.season");
    }

    public async Task<PublicSeasonDto> GetSeasonAsync(string key, CancellationToken ct)
    {
        var season = await ResolveSeasonAsync(key, ct);

        var groupName = await _db.Groups
            .AsNoTracking()
            .Where(g => g.Id == season.GroupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var rounds = await _db.Rounds
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.SeasonId == season.Id
                && r.GroupId == season.GroupId
                && VisibleStates.Contains(r.Status))
            .OrderByDescending(r => r.Number)
            .Select(r => new PublicRoundSummaryDto
            {
                Number = r.Number,
                Title = r.Title,
                Status = r.Status,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                IsScored = r.Status == RoundStatus.Scored,
            })
            .ToListAsync(ct);

        var ruleSet = await _config.GetRuleSetAsync(season.Id, ct);

        return new PublicSeasonDto
        {
            GroupName = groupName,
            SeasonName = season.Name,
            TournamentType = season.TournamentType,
            Rounds = rounds,
            Ruleset = new PublicRulesetDto
            {
                ColumnOnly = ruleSet.BasePointsFor(ScoreCategory.ColumnOnly),
                Traditional = ruleSet.BasePointsFor(ScoreCategory.Traditional),
                Medium = ruleSet.BasePointsFor(ScoreCategory.Medium),
                Uncommon = ruleSet.BasePointsFor(ScoreCategory.Uncommon),
                ExtraUncommon = ruleSet.BasePointsFor(ScoreCategory.ExtraUncommon),
            },
        };
    }

    public async Task<IReadOnlyList<PublicStandingRowDto>> GetStandingsAsync(string key, CancellationToken ct)
    {
        var season = await ResolveSeasonAsync(key, ct);

        // Elimination is a per-group membership flag, read from the season's own group.
        var eliminated = (await _db.GroupUsers
            .AsNoTracking()
            .Where(gu => gu.GroupId == season.GroupId && gu.IsEliminated)
            .Select(gu => gu.UserId)
            .ToListAsync(ct)).ToHashSet();

        var rows = await _db.Standings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.SeasonId == season.Id && s.GroupId == season.GroupId)
            .OrderBy(s => s.Position)
            .Join(_db.Users, s => s.UserId, u => u.Id, (s, u) => new PublicStandingRowDto
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

        var history = await HistoryAsync(season, ct);

        foreach (var row in rows)
        {
            row.IsEliminated = eliminated.Contains(row.UserId);
            if (history.TryGetValue(row.UserId, out var rounds))
            {
                row.Rounds = rounds;
            }
        }

        return rows;
    }

    /// <summary>
    /// Every participant's per-round result for the season, in one query — the history is what
    /// turns an accumulated total into something a reader can check. Only scored rounds have a
    /// persisted result, which is also the only thing the official standings counted.
    /// </summary>
    private async Task<Dictionary<Guid, List<PublicStandingRoundDto>>> HistoryAsync(
        Season season, CancellationToken ct)
    {
        var entries = await _db.RoundParticipantResults
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.GroupId == season.GroupId && r.SeasonId == season.Id)
            .Join(
                _db.Rounds.AsNoTracking().IgnoreQueryFilters()
                    .Where(round => round.SeasonId == season.Id
                        && round.GroupId == season.GroupId
                        && round.Status == RoundStatus.Scored),
                result => result.RoundId,
                round => round.Id,
                (result, round) => new
                {
                    result.UserId,
                    round.Number,
                    result.FinalPoints,
                    result.WasAbsent,
                    result.FlavioRuleApplied,
                })
            .ToListAsync(ct);

        return entries
            .GroupBy(e => e.UserId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(e => e.Number)
                    .Select(e => new PublicStandingRoundDto
                    {
                        Number = e.Number,
                        Points = e.FinalPoints,
                        WasAbsent = e.WasAbsent,
                        FlavioRuleApplied = e.FlavioRuleApplied,
                    })
                    .ToList());
    }

    public async Task<PublicRoundDto> GetRoundAsync(string key, int roundNumber, CancellationToken ct)
    {
        var season = await ResolveSeasonAsync(key, ct);

        var round = await _db.Rounds
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.SeasonId == season.Id
                && r.GroupId == season.GroupId
                && r.Number == roundNumber
                && VisibleStates.Contains(r.Status), ct)
            ?? throw new NotFoundException("notFound.round");

        var ruleSet = await _config.GetRuleSetAsync(season.Id, ct);
        var orderedMatches = round.Matches.OrderBy(m => m.Order).ThenBy(m => m.StartsAt).ToList();

        // A match contributes as soon as it carries a score, matching the temporary standings.
        var computable = orderedMatches
            .Where(m => (m.Status == MatchStatus.InProgress || m.Status == MatchStatus.Finished)
                && m.HomeScore is not null && m.AwayScore is not null)
            .ToList();
        var dismissed = orderedMatches.Count(m => m.Status is MatchStatus.Postponed or MatchStatus.Cancelled);

        var dto = new PublicRoundDto
        {
            Number = round.Number,
            Title = round.Title,
            Status = round.Status,
            IsPartial = round.Status != RoundStatus.Scored,
            ComputedMatches = computable.Count,
            RemainingMatches = Math.Max(0, orderedMatches.Count - computable.Count - dismissed),
            LastUpdatedAt = round.ResultsUpdatedAt
                ?? orderedMatches
                    .Where(m => m.LastResultUpdatedAt is not null)
                    .Max(m => (DateTime?)m.LastResultUpdatedAt),
            Matches = orderedMatches.Select(m =>
            {
                var isClassic = ruleSet.IsClassicPair(m.HomeTeamId, m.AwayTeamId);
                return new RoundResultMatchDto
                {
                    RoundMatchId = m.Id,
                    Competition = m.Competition,
                    Phase = m.Phase,
                    HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
                    AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
                    HomeScore = m.HomeScore,
                    AwayScore = m.AwayScore,
                    IsFinished = m.IsFinished,
                    IsClassic = isClassic,
                    IsManualMultiplier = m.ManualMultiplierOverride is not null,
                    Multiplier = m.ManualMultiplierOverride ?? _scoring.GetMultiplier(
                        ruleSet, m.Competition, m.Phase, isClassic),
                };
            }).ToList(),
        };

        var predictions = await _db.Predictions
            .AsNoTracking()
            .Where(p => p.RoundId == round.Id)
            .ToListAsync(ct);

        dto.Participants.AddRange(round.Status == RoundStatus.Scored
            ? await ScoredParticipantsAsync(round.Id, predictions, ct)
            : await LiveParticipantsAsync(season, ruleSet, computable, predictions, ct));

        return dto;
    }

    /// <summary>
    /// Participants of a scored round, read from what the scoring pass persisted. Nothing is
    /// recomputed: the audit has to show the numbers that actually count, including the
    /// Flávio Rule reduction and absence penalties that the live path never applies.
    /// </summary>
    private async Task<List<PublicParticipantScoreDto>> ScoredParticipantsAsync(
        Guid roundId, List<Prediction> predictions, CancellationToken ct)
    {
        var results = await _db.RoundParticipantResults
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.RoundId == roundId)
            .ToListAsync(ct);

        var scores = await _db.PredictionScores
            .AsNoTracking()
            .Where(s => s.RoundId == roundId)
            .ToListAsync(ct);

        var names = await NamesAsync(results.Select(r => r.UserId), ct);
        var predictionsByUser = predictions
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(p => p.RoundMatchId));

        return results
            .Select(result =>
            {
                predictionsByUser.TryGetValue(result.UserId, out var userPredictions);
                return new PublicParticipantScoreDto
                {
                    UserId = result.UserId,
                    Name = names.GetValueOrDefault(result.UserId, string.Empty),
                    GrossPoints = result.GrossPoints,
                    FinalPoints = result.FinalPoints,
                    PenaltyPoints = result.PenaltyPoints,
                    WasAbsent = result.WasAbsent,
                    WasEliminated = result.WasEliminated,
                    FlavioRuleApplied = result.FlavioRuleApplied,
                    MatchScores = scores
                        .Where(s => s.UserId == result.UserId)
                        .Select(s => ToMatchScore(s, userPredictions))
                        .ToList(),
                };
            })
            .OrderByDescending(p => p.FinalPoints)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Participants of a locked-but-unscored round, computed on demand from the results
    /// available so far — the same arithmetic as the temporary standings. Absences,
    /// eliminations and the Flávio Rule are deliberately left out: they are decided when the
    /// round is scored, and anticipating them here would contradict the official result.
    /// </summary>
    private async Task<List<PublicParticipantScoreDto>> LiveParticipantsAsync(
        Season season,
        ScoringRuleSet ruleSet,
        List<RoundMatch> computable,
        List<Prediction> predictions,
        CancellationToken ct)
    {
        var predicted = predictions.Select(p => p.UserId).Distinct().ToList();
        var participants = await GroupQueries.ActiveParticipants(_db, season.GroupId)
            .Where(u => predicted.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(ct);

        var rows = new List<PublicParticipantScoreDto>();
        foreach (var participant in participants)
        {
            var userPredictions = predictions
                .Where(p => p.UserId == participant.Id)
                .ToDictionary(p => p.RoundMatchId);

            var matchScores = new List<PublicMatchScoreDto>();
            var total = 0;

            foreach (var match in computable)
            {
                if (!userPredictions.TryGetValue(match.Id, out var prediction))
                {
                    continue;
                }

                var actualHome = match.HomeScore!.Value;
                var actualAway = match.AwayScore!.Value;
                var result = _scoring.ScorePrediction(
                    ruleSet,
                    prediction.PredictedHomeScore, prediction.PredictedAwayScore,
                    actualHome, actualAway,
                    match.Competition, match.Phase,
                    ruleSet.IsClassicPair(match.HomeTeamId, match.AwayTeamId));

                // A manual override replaces the rule-based multiplier, exactly as the
                // official scoring pass does — ScorePrediction knows nothing about it.
                var multiplier = match.ManualMultiplierOverride ?? result.Multiplier;
                var finalPoints = result.BasePoints * multiplier;
                total += finalPoints;

                matchScores.Add(new PublicMatchScoreDto
                {
                    RoundMatchId = match.Id,
                    PredictedHomeScore = prediction.PredictedHomeScore,
                    PredictedAwayScore = prediction.PredictedAwayScore,
                    BasePoints = result.BasePoints,
                    Multiplier = multiplier,
                    FinalPoints = finalPoints,
                    ScoreCategory = result.Category,
                    IsExactScore = result.IsExactScore,
                    IsCorrectColumn = result.IsCorrectColumn,
                });
            }

            rows.Add(new PublicParticipantScoreDto
            {
                UserId = participant.Id,
                Name = participant.Name,
                GrossPoints = total,
                FinalPoints = total,
                MatchScores = matchScores,
            });
        }

        return rows
            .OrderByDescending(p => p.FinalPoints)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PublicMatchScoreDto ToMatchScore(
        PredictionScore score, Dictionary<Guid, Prediction>? userPredictions)
    {
        Prediction? prediction = null;
        userPredictions?.TryGetValue(score.RoundMatchId, out prediction);

        return new PublicMatchScoreDto
        {
            RoundMatchId = score.RoundMatchId,
            PredictedHomeScore = prediction?.PredictedHomeScore,
            PredictedAwayScore = prediction?.PredictedAwayScore,
            BasePoints = score.BasePoints,
            Multiplier = score.Multiplier,
            FinalPoints = score.FinalPoints,
            ScoreCategory = score.ScoreCategory,
            IsExactScore = score.IsExactScore,
            IsCorrectColumn = score.IsCorrectColumn,
        };
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        return await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }
}
