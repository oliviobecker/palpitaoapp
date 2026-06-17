using Palpitao.Api.DTOs.Scoring;

namespace Palpitao.Api.Services.Scoring;

public interface IRoundScoringService
{
    /// <summary>Registers the final result of a match (admin only).</summary>
    Task SetMatchResultAsync(Guid matchId, MatchResultRequest request, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Scores a round: validates results, computes per-match points, applies
    /// multipliers, absences and the Flávio rule, saves the per-participant
    /// results and updates the season standing. Sets the round to Scored.
    /// </summary>
    Task<RoundResultsDto> ScoreRoundAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    Task<RoundResultsDto> GetRoundResultsAsync(Guid roundId, CancellationToken ct);

    /// <summary>
    /// Recalculates the whole season from scratch in a safe, idempotent way:
    /// clears previous calculations, re-scores the already-finished rounds in
    /// order, reapplies absences/penalties and rebuilds the standing.
    /// </summary>
    Task RecalculateSeasonAsync(Guid seasonId, Guid actingUserId, CancellationToken ct);
}
