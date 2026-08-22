using Palpitao.Api.DTOs.Absences;

namespace Palpitao.Api.Services.Absences;

/// <summary>Outcome of processing one participant's absence in a round.</summary>
public record AbsenceOutcome(Guid UserId, int AbsenceNumber, int PenaltyPoints, bool Eliminated);

public interface IAbsenceService
{
    /// <summary>Participant is absent when they did not submit every required prediction.</summary>
    Task<bool> IsAbsentAsync(Guid roundId, Guid userId, CancellationToken ct);

    /// <summary>Active, non-eliminated participants considered absent in the round.</summary>
    Task<IReadOnlyList<Guid>> DetectAbsenteesAsync(Guid roundId, CancellationToken ct);

    /// <summary>Number of absences a participant has accumulated in the season.</summary>
    Task<int> CountSeasonAbsencesAsync(Guid seasonId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Detects absentees of a round and applies the punishment rules (records
    /// the absence, removes points on the 3rd/4th and eliminates on the 5th).
    /// Idempotent: re-processing a round replaces its previous absence records.
    /// </summary>
    Task<IReadOnlyList<AbsenceOutcome>> ProcessRoundAbsencesAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    Task ApplyOverrideAsync(Guid roundId, AbsenceOverrideRequest request, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Rounds of the group's active season that already closed for predictions (Locked or
    /// Scored) where the participant did not complete their predictions and is not already
    /// forced absent — i.e. what an admin can record them absent for when (re)activating them.
    /// </summary>
    Task<IReadOnlyList<AbsenceCandidateRoundDto>> GetAbsenceCandidateRoundsAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Stages <c>IsAbsent = true</c> overrides for the given rounds <b>without saving</b>, so the
    /// caller commits them in the same transaction as its own changes (all services share the
    /// request-scoped <c>AppDbContext</c>). Every id is validated against
    /// <see cref="GetAbsenceCandidateRoundsAsync"/>; ineligible ids are rejected.
    /// </summary>
    Task StageAbsenceOverridesAsync(
        Guid userId, IReadOnlyCollection<Guid> roundIds, string justification, Guid actingUserId, CancellationToken ct);

    Task ReactivateAsync(
        Guid userId, string justification, IReadOnlyCollection<Guid>? absentRoundIds, Guid actingUserId, CancellationToken ct);

    Task<IReadOnlyList<AbsenceDto>> GetUserAbsencesAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<AbsenceDto>> GetRoundAbsencesAsync(Guid roundId, CancellationToken ct);
}
