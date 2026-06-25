using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Domain service that applies the Palpitão scoring rules: columns, exact-score
/// categories, base points and per-match multipliers. The rule-driven methods take a
/// resolved <see cref="ScoringRuleSet"/> (per-season config or tournament-type defaults),
/// so the engine itself stays stateless and the rules are configurable per season.
/// </summary>
public interface IScoringService
{
    /// <summary>Determines the column (Home / Draw / Away) of a score.</summary>
    ScoreColumn GetColumn(int homeScore, int awayScore);

    /// <summary>True when the prediction matches the column of the real result.</summary>
    bool IsCorrectColumn(int predictedHome, int predictedAway, int actualHome, int actualAway);

    /// <summary>True when the prediction equals the real result exactly.</summary>
    bool IsExactScore(int predictedHome, int predictedAway, int actualHome, int actualAway);

    /// <summary>
    /// Difficulty category of an exact score (Traditional / Medium / Uncommon /
    /// ExtraUncommon), per the ruleset. Assumes the score is treated as an exact hit.
    /// </summary>
    ScoreCategory GetExactScoreCategory(ScoringRuleSet ruleSet, int homeScore, int awayScore);

    /// <summary>
    /// Resulting category of a prediction: an exact-score difficulty category,
    /// <see cref="ScoreCategory.ColumnOnly"/> or <see cref="ScoreCategory.None"/>.
    /// </summary>
    ScoreCategory GetCategory(ScoringRuleSet ruleSet, int predictedHome, int predictedAway, int actualHome, int actualAway);

    /// <summary>Base points awarded for a given category (before the multiplier).</summary>
    int GetBasePoints(ScoringRuleSet ruleSet, ScoreCategory category);

    /// <summary>
    /// Multiplier of a match: the ruleset's value for the (competition, phase), using the
    /// classic value when both teams are classic-eligible (a Big Seven derby for England /
    /// a knockout between two world champions for the World Cup).
    /// </summary>
    int GetMultiplier(ScoringRuleSet ruleSet, Competition competition, MatchPhase phase, bool homeIsClassic, bool awayIsClassic);

    /// <summary>Full scoring breakdown of a prediction against the real result.</summary>
    PredictionScoreResult ScorePrediction(
        ScoringRuleSet ruleSet,
        int predictedHome,
        int predictedAway,
        int actualHome,
        int actualAway,
        Competition competition,
        MatchPhase phase,
        bool homeIsClassic,
        bool awayIsClassic);
}
