using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Full scoring breakdown of a single prediction against a match result.
/// </summary>
public record PredictionScoreResult(
    ScoreColumn ActualColumn,
    bool IsCorrectColumn,
    bool IsExactScore,
    ScoreCategory Category,
    int BasePoints,
    int Multiplier,
    int FinalPoints);
