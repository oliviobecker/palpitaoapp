using Palpitao.Api.DTOs.Absences;

namespace Palpitao.Api.Services.Absences;

/// <summary>Outcome of processing one participant's absence in a round.</summary>
public record AbsenceOutcome(Guid UserId, int AbsenceNumber, int PenaltyPoints, bool Eliminated);

/// <summary>
/// Who is absent in a round, in the round alone and for the whole round played in parts
/// ("10.1" + "10.2" count as one round for absences). On a standalone round both sets agree.
/// On a part, <see cref="WeekAbsentees"/> stays empty unless this part decides — it is the last
/// one — and then holds only those who also sent nothing in every other part with matches.
/// </summary>
/// <param name="PartAbsentees">Absent in this round alone (override, else "sent nothing"), in roster order.</param>
/// <param name="WeekAbsentees">Absent for the whole round: the ones scoring records as absences.</param>
/// <param name="IsPart">The round is a part of a round played in parts.</param>
/// <param name="DecidesWeek">No later part exists, so scoring this round decides the absences.</param>
/// <param name="OtherPartsClosed">Every other part is closed for predictions (trivially true when standalone).</param>
public record WeekAbsence(
    IReadOnlyList<Guid> PartAbsentees,
    IReadOnlySet<Guid> WeekAbsentees,
    bool IsPart,
    bool DecidesWeek,
    bool OtherPartsClosed);

/// <summary>
/// What an absence review staged: the active season the rounds belong to, the rounds whose
/// effective state changed and whether any of them is already Scored — in which case only a
/// season recalculation makes the change (and everyone's absence ladder) real.
/// </summary>
public record AbsenceReviewStaging(Guid? SeasonId, IReadOnlyList<Guid> ChangedRoundIds, bool RequiresRecalculation);

public interface IAbsenceService
{
    /// <summary>Participant is absent when they submitted no prediction at all for the round.</summary>
    Task<bool> IsAbsentAsync(Guid roundId, Guid userId, CancellationToken ct);

    /// <summary>Active, non-eliminated participants considered absent in the round.</summary>
    Task<IReadOnlyList<Guid>> DetectAbsenteesAsync(Guid roundId, CancellationToken ct);

    /// <summary>
    /// <see cref="DetectAbsenteesAsync"/> plus the verdict for the whole round when the round is
    /// a part of a round played in parts: only the last part decides, and only someone absent in
    /// every part (parts without matches are neutral) counts as absent.
    /// </summary>
    Task<WeekAbsence> DetectWeekAbsenteesAsync(Guid roundId, CancellationToken ct);

    /// <summary>Number of absences a participant has accumulated in the season.</summary>
    Task<int> CountSeasonAbsencesAsync(Guid seasonId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Detects absentees of a round and applies the punishment rules (records
    /// the absence, removes points on the 3rd/4th and eliminates on the 5th).
    /// Idempotent: re-processing a round replaces its previous absence records.
    /// On a part of a round played in parts, whoever sent nothing in this part alone gets a
    /// zeroed result that is not an absence; only the last part records absences, for those
    /// who missed every part.
    /// </summary>
    Task<IReadOnlyList<AbsenceOutcome>> ProcessRoundAbsencesAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    Task ApplyOverrideAsync(Guid roundId, AbsenceOverrideRequest request, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Rounds of the group's active season that already closed for predictions (Locked or
    /// Scored) where the participant sent no prediction at all and is not already forced
    /// absent — i.e. what an admin can record them absent for when (re)activating them.
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

    /// <summary>
    /// Rounds of the group's active season already closed for predictions (Locked or Scored) in
    /// which the participant currently counts as absent, or carries an override — what an admin
    /// reviews to excuse absences (e.g. rounds before the participant actually joined) or to
    /// restore one that was excused.
    /// </summary>
    Task<IReadOnlyList<AbsenceReviewRoundDto>> GetAbsenceReviewRoundsAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Stages one override per round whose decision differs from its effective state,
    /// <b>without saving</b> (same contract as <see cref="StageAbsenceOverridesAsync"/>). Every
    /// id must come from <see cref="GetAbsenceReviewRoundsAsync"/>; anything else is rejected.
    /// </summary>
    Task<AbsenceReviewStaging> StageAbsenceReviewAsync(
        Guid userId, IReadOnlyCollection<AbsenceReviewDecision> decisions, string justification,
        Guid actingUserId, CancellationToken ct);

    Task<IReadOnlyList<AbsenceDto>> GetUserAbsencesAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<AbsenceDto>> GetRoundAbsencesAsync(Guid roundId, CancellationToken ct);
}
