using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Rounds;

public class CreateRoundRequest
{
    public Guid SeasonId { get; set; }

    public int Number { get; set; }

    public string? Title { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Plays the new round as the next part of the previous round ("10.2" after "10"): it is
    /// created at <see cref="Number"/> and then joined, exactly like the round-detail action.
    /// </summary>
    public bool JoinPreviousWeek { get; set; }
}

public class UpdateRoundRequest
{
    public int Number { get; set; }

    public string? Title { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}

public class RoundSummaryDto
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public int Number { get; set; }

    /// <summary>0 for a standalone round; 1..k for the parts of a round played in parts.</summary>
    public int Part { get; set; }

    public string? Title { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public RoundStatus Status { get; set; }
    public DateTime? FirstMatchStartsAt { get; set; }

    /// <summary>General lock: one minute before the first kickoff. Computed on serialization.</summary>
    public DateTime? PredictionDeadlineUtc => PredictionDeadline.From(FirstMatchStartsAt);

    public DateTime? PublishedAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public int MatchCount { get; set; }

    /// <summary>From the round's season: the certame type (drives allowed competitions/phases).</summary>
    public TournamentType TournamentType { get; set; }

    /// <summary>From the round's season: whether participants may view others' predictions.</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>From the round's season: whether participants submit predictions in the app.</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>From the round's season: whether FA Cup fixtures may be added to this round.</summary>
    public bool FaCupEnabled { get; set; } = true;
}

public class RoundDto
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public int Number { get; set; }

    /// <summary>0 for a standalone round; 1..k for the parts of a round played in parts.</summary>
    public int Part { get; set; }

    public string? Title { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public RoundStatus Status { get; set; }
    public DateTime? FirstMatchStartsAt { get; set; }

    /// <summary>General lock: one minute before the first kickoff. Computed on serialization.</summary>
    public DateTime? PredictionDeadlineUtc => PredictionDeadline.From(FirstMatchStartsAt);

    public DateTime? PublishedAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? MirrorPublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MatchDto> Matches { get; set; } = new();

    /// <summary>From the round's season: the certame type (drives allowed competitions/phases).</summary>
    public TournamentType TournamentType { get; set; }

    /// <summary>From the round's season: whether participants may view others' predictions.</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>From the round's season: whether participants submit predictions in the app.</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>From the round's season: whether FA Cup fixtures may be added to this round.</summary>
    public bool FaCupEnabled { get; set; } = true;

    /// <summary>Flávio-rule info for the group message (null when not applicable).</summary>
    public RoundFlavioDto? Flavio { get; set; }

    /// <summary>
    /// The round's place among the rounds sharing its number, and what the admin can do about
    /// it (join the previous round, leave, finalize).
    /// </summary>
    public RoundWeekDto Week { get; set; } = new();
}

/// <summary>
/// A round played in parts counts as one round for absences: only its last part decides who
/// missed it. This carries what the round-detail screen needs to explain that and to offer the
/// join/leave actions with the right warnings.
/// </summary>
public class RoundWeekDto
{
    /// <summary>Every round sharing the number (cancelled ones included), in part order.</summary>
    public List<RoundWeekPartDto> Parts { get; set; } = new();

    /// <summary>True for a standalone round or the last part: its scoring decides absences.</summary>
    public bool DecidesAbsences { get; set; }

    /// <summary>
    /// This round decides absences but another part is still Draft/Published, so it cannot be
    /// finalized yet (the admin locks or cancels that part first).
    /// </summary>
    public bool OpenPartBlocksFinalize { get; set; }

    /// <summary>A later part is already Scored: finalizing this one recalculates the season.</summary>
    public bool LaterPartScored { get; set; }

    public RoundWeekMoveDto JoinPrevious { get; set; } = new();

    public RoundWeekMoveDto Leave { get; set; } = new();
}

public class RoundWeekPartDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public int Part { get; set; }
    public RoundStatus Status { get; set; }
}

/// <summary>Preview of a join/leave: whether it is allowed, where the round lands and its cost.</summary>
public class RoundWeekMoveDto
{
    public bool Allowed { get; set; }

    public int? TargetNumber { get; set; }

    public int? TargetPart { get; set; }

    /// <summary>Rounds whose number or part changes, the moved round included.</summary>
    public int RenumberedRounds { get; set; }

    /// <summary>A Scored round is affected, so the season is recalculated in the same transaction.</summary>
    public bool RequiresRecalculation { get; set; }
}

public class RoundFlavioDto
{
    /// <summary>True from round 16 onwards.</summary>
    public bool Applies { get; set; }

    /// <summary>Current leader(s) of the season standings (may be empty early on).</summary>
    public List<string> LeaderNames { get; set; } = new();

    /// <summary>Leader's special deadline; null until the round is published.</summary>
    public DateTime? DeadlineUtc { get; set; }

    /// <summary>
    /// The rule's window in hours (24, or 12 when the round was published less than a day
    /// before the first match); null until the round is published. This is what the group
    /// message announces ("tem até 24 horas para palpitar").
    /// </summary>
    public int? WindowHours { get; set; }

    /// <summary>
    /// True when the general lock (one minute before the first kickoff) cut the window
    /// short, so <see cref="DeadlineUtc"/> is earlier than reference + <see cref="WindowHours"/>.
    /// </summary>
    public bool DeadlineCappedByLock { get; set; }
}
