using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Default scoring values — the historical hardcoded Palpitão rules. Single source of
/// truth used to (a) seed a new <see cref="Entities.SeasonScoringConfig"/> and (b) score
/// seasons that have not customised their config yet. The "defaults reproduce the classic
/// numbers" regression tests pin these so every consumer stays correct.
/// </summary>
public static class ScoringDefaults
{
    /// <summary>Base points per category (identical for every tournament type).</summary>
    public static IReadOnlyDictionary<ScoreCategory, int> BasePoints() => new Dictionary<ScoreCategory, int>
    {
        [ScoreCategory.ColumnOnly] = 1,
        [ScoreCategory.Traditional] = 3,
        [ScoreCategory.Medium] = 5,
        [ScoreCategory.Uncommon] = 7,
        [ScoreCategory.ExtraUncommon] = 10,
    };

    /// <summary>
    /// Exact-score → category mapping, normalized as (min, max) since the categories are
    /// symmetric. Any score not listed is <see cref="ScoreCategory.ExtraUncommon"/>. Same
    /// taxonomy for England and the World Cup.
    /// </summary>
    public static IReadOnlyList<(int Low, int High, ScoreCategory Category)> ScoreCategories() => new[]
    {
        (1, 1, ScoreCategory.Traditional), (0, 1, ScoreCategory.Traditional), (0, 2, ScoreCategory.Traditional), (1, 2, ScoreCategory.Traditional),
        (0, 0, ScoreCategory.Medium), (2, 2, ScoreCategory.Medium), (1, 3, ScoreCategory.Medium), (0, 3, ScoreCategory.Medium),
        (2, 3, ScoreCategory.Uncommon), (0, 4, ScoreCategory.Uncommon), (1, 4, ScoreCategory.Uncommon), (3, 3, ScoreCategory.Uncommon), (2, 4, ScoreCategory.Uncommon),
    };

    /// <summary>
    /// Default multiplier rows for a tournament type. <c>Normal</c> applies always;
    /// <c>Classic</c> applies when both teams are classic-eligible. The England rows encode
    /// "phase wins, no accumulation" (FA Cup final = 3 even for a derby) by setting equal
    /// values; the World Cup rows encode "group-stage classics are not doubled".
    /// </summary>
    public static IReadOnlyList<(Competition Competition, MatchPhase Phase, int Normal, int Classic)> MultiplierRules(TournamentType type)
        => type == TournamentType.FifaWorldCup
            ? new[]
            {
                (Competition.FifaWorldCup, MatchPhase.WorldCupGroupStage, 1, 1),
                (Competition.FifaWorldCup, MatchPhase.WorldCupRoundOf32, 2, 4),
                (Competition.FifaWorldCup, MatchPhase.WorldCupRoundOf16, 2, 4),
                (Competition.FifaWorldCup, MatchPhase.WorldCupQuarterFinal, 3, 6),
                (Competition.FifaWorldCup, MatchPhase.WorldCupSemiFinal, 3, 6),
                (Competition.FifaWorldCup, MatchPhase.WorldCupThirdPlace, 3, 6),
                (Competition.FifaWorldCup, MatchPhase.WorldCupFinal, 3, 6),
            }
            : new[]
            {
                (Competition.PremierLeague, MatchPhase.Regular, 1, 2),
                (Competition.FACup, MatchPhase.Regular, 1, 2),
                (Competition.FACup, MatchPhase.FACupSemiFinal, 2, 2),
                (Competition.FACup, MatchPhase.FACupFinal, 3, 3),
                (Competition.Championship, MatchPhase.Regular, 1, 1),
                (Competition.Championship, MatchPhase.PlayoffSemiFinal, 2, 2),
                (Competition.Championship, MatchPhase.PlayoffFinal, 2, 2),
                (Competition.LeagueOne, MatchPhase.Regular, 2, 2),
            };

    /// <summary>Builds a default <see cref="ScoringRuleSet"/> for a tournament type.</summary>
    public static ScoringRuleSet ForTournamentType(TournamentType type, IReadOnlySet<Guid> classicTeamIds)
    {
        var scoreCategories = ScoreCategories()
            .ToDictionary(e => (e.Low, e.High), e => e.Category);
        var multipliers = MultiplierRules(type)
            .ToDictionary(r => (r.Competition, r.Phase), r => (r.Normal, r.Classic));
        return new ScoringRuleSet(BasePoints(), scoreCategories, multipliers, classicTeamIds);
    }
}
