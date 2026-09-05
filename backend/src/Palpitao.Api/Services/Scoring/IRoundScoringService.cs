using Palpitao.Api.DTOs.Absences;
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

    /// <summary>
    /// Applies an admin's absence review for one participant (one override per round whose
    /// decision differs from today's state) and, when an already-scored round changed,
    /// recalculates the season in the same transaction so the absence ladder, penalties and
    /// eliminations shift for everyone. Changes to Locked rounds are only stored: they land
    /// when the round is scored.
    /// </summary>
    Task<AbsenceReviewResultDto> ReviewParticipantAbsencesAsync(
        Guid userId, AbsenceReviewRequest request, Guid actingUserId, CancellationToken ct);
}
