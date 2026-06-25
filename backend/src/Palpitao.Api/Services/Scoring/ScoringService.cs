using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Applies the Palpitão scoring rules over a resolved <see cref="ScoringRuleSet"/>.
///
/// Base points and the exact-score categories come from the ruleset (per-season config or
/// <see cref="ScoringDefaults"/>); the column/exact/wrong determination is pure. Final
/// points = base points * match multiplier, and a wrong prediction scores 0 even when the
/// match has a multiplier (base points are already 0). Stateless — registered as a
/// singleton.
/// </summary>
public class ScoringService : IScoringService
{
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

    public ScoreCategory GetExactScoreCategory(ScoringRuleSet ruleSet, int homeScore, int awayScore)
        => ruleSet.CategoryForExactScore(homeScore, awayScore);

    public ScoreCategory GetCategory(ScoringRuleSet ruleSet, int predictedHome, int predictedAway, int actualHome, int actualAway)
    {
        if (IsExactScore(predictedHome, predictedAway, actualHome, actualAway))
        {
            return ruleSet.CategoryForExactScore(actualHome, actualAway);
        }

        if (IsCorrectColumn(predictedHome, predictedAway, actualHome, actualAway))
        {
            return ScoreCategory.ColumnOnly;
        }

        return ScoreCategory.None;
    }

    public int GetBasePoints(ScoringRuleSet ruleSet, ScoreCategory category)
        => category == ScoreCategory.None ? 0 : ruleSet.BasePointsFor(category);

    public int GetMultiplier(ScoringRuleSet ruleSet, Competition competition, MatchPhase phase, bool homeIsClassic, bool awayIsClassic)
        => ruleSet.MultiplierFor(competition, phase, homeIsClassic && awayIsClassic);

    public PredictionScoreResult ScorePrediction(
        ScoringRuleSet ruleSet,
        int predictedHome,
        int predictedAway,
        int actualHome,
        int actualAway,
        Competition competition,
        MatchPhase phase,
        bool homeIsClassic,
        bool awayIsClassic)
    {
        var actualColumn = GetColumn(actualHome, actualAway);
        var isExact = IsExactScore(predictedHome, predictedAway, actualHome, actualAway);
        var isCorrectColumn = IsCorrectColumn(predictedHome, predictedAway, actualHome, actualAway);
        var category = GetCategory(ruleSet, predictedHome, predictedAway, actualHome, actualAway);
        var basePoints = GetBasePoints(ruleSet, category);
        var multiplier = GetMultiplier(ruleSet, competition, phase, homeIsClassic, awayIsClassic);

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
