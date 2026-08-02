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
