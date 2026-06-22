using Palpitao.Api.Enums;
using Palpitao.Api.Services.Tournaments;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Implements the Palpitão England 2025/2026 scoring rules.
///
/// Base points:
///   - Correct column only ......... 1
///   - Exact score (Traditional) ... 3
///   - Exact score (Medium) ........ 5
///   - Exact score (Uncommon) ...... 7
///   - Exact score (ExtraUncommon) . 10
///   - Wrong ....................... 0
/// Final points = base points * match multiplier.
/// </summary>
public class ScoringService : IScoringService
{
    // Score sets are stored normalized as (min, max) because every category is
    // symmetric (e.g. 1x0 and 0x1 share the same difficulty).
    private static readonly HashSet<(int, int)> TraditionalScores = new()
    {
        (1, 1), (0, 1), (0, 2), (1, 2),
    };

    private static readonly HashSet<(int, int)> MediumScores = new()
    {
        (0, 0), (2, 2), (1, 3), (0, 3),
    };

    private static readonly HashSet<(int, int)> UncommonScores = new()
    {
        (2, 3), (0, 4), (1, 4), (3, 3), (2, 4),
    };

    public ScoreColumn GetColumn(int homeScore, int awayScore)
    {
        if (homeScore > awayScore)
        {
            return ScoreColumn.Home;
        }

        return homeScore < awayScore ? ScoreColumn.Away : ScoreColumn.Draw;
    }

    public bool IsCorrectColumn(int predictedHome, int predictedAway, int actualHome, int actualAway)
        => GetColumn(predictedHome, predictedAway) == GetColumn(actualHome, actualAway);

    public bool IsExactScore(int predictedHome, int predictedAway, int actualHome, int actualAway)
        => predictedHome == actualHome && predictedAway == actualAway;

    public ScoreCategory GetExactScoreCategory(int homeScore, int awayScore)
    {
        var key = (Math.Min(homeScore, awayScore), Math.Max(homeScore, awayScore));

        if (TraditionalScores.Contains(key))
        {
            return ScoreCategory.Traditional;
        }

        if (MediumScores.Contains(key))
        {
            return ScoreCategory.Medium;
        }

        if (UncommonScores.Contains(key))
        {
            return ScoreCategory.Uncommon;
        }

        return ScoreCategory.ExtraUncommon;
    }

    public ScoreCategory GetCategory(int predictedHome, int predictedAway, int actualHome, int actualAway)
    {
        if (IsExactScore(predictedHome, predictedAway, actualHome, actualAway))
        {
            return GetExactScoreCategory(actualHome, actualAway);
        }

        if (IsCorrectColumn(predictedHome, predictedAway, actualHome, actualAway))
        {
            return ScoreCategory.ColumnOnly;
        }

        return ScoreCategory.None;
    }

    public int GetBasePoints(ScoreCategory category) => category switch
    {
        ScoreCategory.None => 0,
        ScoreCategory.ColumnOnly => 1,
        ScoreCategory.Traditional => 3,
        ScoreCategory.Medium => 5,
        ScoreCategory.Uncommon => 7,
        ScoreCategory.ExtraUncommon => 10,
        _ => 0,
    };

    public int GetMultiplier(
        Competition competition,
        MatchPhase phase,
        bool homeIsBigSeven,
        bool awayIsBigSeven,
        bool homeIsWorldChampion = false,
        bool awayIsWorldChampion = false)
    {
        var isBigSevenDerby = homeIsBigSeven && awayIsBigSeven;

        return competition switch
        {
            // Premier League: Big Seven derbies are classics worth double.
            Competition.PremierLeague => isBigSevenDerby ? 2 : 1,

            // FA Cup: phase multiplier wins; otherwise a Big Seven derby doubles.
            Competition.FACup => phase switch
            {
                MatchPhase.FACupFinal => 3,
                MatchPhase.FACupSemiFinal => 2,
                _ => isBigSevenDerby ? 2 : 1,
            },

            // Championship: playoffs (semi and final) are worth double.
            Competition.Championship => phase switch
            {
                MatchPhase.PlayoffSemiFinal => 2,
                MatchPhase.PlayoffFinal => 2,
                _ => 1,
            },

            // League One: every registered match is worth double.
            Competition.LeagueOne => 2,

            // FIFA World Cup: phase multiplier, doubled for knockout classics
            // (both sides world champions).
            Competition.FifaWorldCup => WorldCupMultiplier(phase, homeIsWorldChampion, awayIsWorldChampion),

            _ => 1,
        };
    }

    /// <summary>
    /// World Cup multiplier: x1 group stage, x2 round of 32/16, x3 from the
    /// quarter-finals on; doubled when a knockout match is a classic between two
    /// world champions (campeãs mundiais). Group-stage classics are NOT doubled.
    /// </summary>
    private static int WorldCupMultiplier(MatchPhase phase, bool homeIsWorldChampion, bool awayIsWorldChampion)
    {
        var phaseMultiplier = phase switch
        {
            MatchPhase.WorldCupGroupStage => 1,
            MatchPhase.WorldCupRoundOf32 or MatchPhase.WorldCupRoundOf16 => 2,
            MatchPhase.WorldCupQuarterFinal
                or MatchPhase.WorldCupSemiFinal
                or MatchPhase.WorldCupThirdPlace
                or MatchPhase.WorldCupFinal => 3,
            _ => 1,
        };

        var isClassic = TournamentRules.IsWorldCupKnockout(phase) && homeIsWorldChampion && awayIsWorldChampion;
        return isClassic ? phaseMultiplier * 2 : phaseMultiplier;
    }

    public PredictionScoreResult ScorePrediction(
        int predictedHome,
        int predictedAway,
        int actualHome,
        int actualAway,
        Competition competition,
        MatchPhase phase,
        bool homeIsBigSeven,
        bool awayIsBigSeven,
        bool homeIsWorldChampion = false,
        bool awayIsWorldChampion = false)
    {
        var actualColumn = GetColumn(actualHome, actualAway);
        var isExact = IsExactScore(predictedHome, predictedAway, actualHome, actualAway);
        var isCorrectColumn = IsCorrectColumn(predictedHome, predictedAway, actualHome, actualAway);
        var category = GetCategory(predictedHome, predictedAway, actualHome, actualAway);
        var basePoints = GetBasePoints(category);
        var multiplier = GetMultiplier(competition, phase, homeIsBigSeven, awayIsBigSeven, homeIsWorldChampion, awayIsWorldChampion);

        // A wrong prediction scores 0 even when the match has a multiplier
        // (base points are already 0, so the product is 0).
        var finalPoints = basePoints * multiplier;

        return new PredictionScoreResult(
            actualColumn,
            isCorrectColumn,
            isExact,
            category,
            basePoints,
            multiplier,
            finalPoints);
    }
}
