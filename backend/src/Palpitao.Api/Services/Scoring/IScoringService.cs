using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Domain service that implements the Palpitão scoring rules: columns, exact
/// score categories, base points and per-match multipliers.
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
    /// ExtraUncommon). Assumes the score is treated as an exact hit.
    /// </summary>
    ScoreCategory GetExactScoreCategory(int homeScore, int awayScore);

    /// <summary>
    /// Resulting category of a prediction: an exact-score difficulty category,
    /// <see cref="ScoreCategory.ColumnOnly"/> or <see cref="ScoreCategory.None"/>.
    /// </summary>
    ScoreCategory GetCategory(int predictedHome, int predictedAway, int actualHome, int actualAway);

    /// <summary>Base points awarded for a given category (before multiplier).</summary>
    int GetBasePoints(ScoreCategory category);

    /// <summary>
    /// Multiplier of a match based on competition, phase and special-match rules
    /// (Big Seven derbies for England; phase + champion-vs-champion classics for the
    /// FIFA World Cup). The <paramref name="homeIsWorldChampion"/>/<paramref name="awayIsWorldChampion"/>
    /// flags only matter for World Cup matches.
    /// </summary>
    int GetMultiplier(
        Competition competition,
        MatchPhase phase,
        bool homeIsBigSeven,
        bool awayIsBigSeven,
        bool homeIsWorldChampion = false,
        bool awayIsWorldChampion = false);

    /// <summary>Full scoring breakdown of a prediction against the real result.</summary>
    PredictionScoreResult ScorePrediction(
        int predictedHome,
        int predictedAway,
        int actualHome,
        int actualAway,
        Competition competition,
        MatchPhase phase,
        bool homeIsBigSeven,
        bool awayIsBigSeven,
        bool homeIsWorldChampion = false,
        bool awayIsWorldChampion = false);
}
