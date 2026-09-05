using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Public;

/// <summary>
/// Identity and navigation of a season reached by its public key. Carries no ids of its
/// own beyond the participants' — rounds are addressed by number, so a shared link stays
/// readable and exposes no internal keys.
/// </summary>
public class PublicSeasonDto
{
    public string GroupName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public TournamentType TournamentType { get; set; }

    /// <summary>Rounds visible on the public link — closed ones only, newest first.</summary>
    public List<PublicRoundSummaryDto> Rounds { get; set; } = new();

    /// <summary>The season's base points, so the page can explain the scoring it shows.</summary>
    public PublicRulesetDto Ruleset { get; set; } = new();
}

public class PublicRoundSummaryDto
{
    public int Number { get; set; }
    public string? Title { get; set; }
    public RoundStatus Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>False while the round is only locked: points are provisional.</summary>
    public bool IsScored { get; set; }
}

/// <summary>
/// A standings row on the public link. Mirrors the in-app <c>StandingDto</c> and adds the
/// round-by-round history: the accumulated total alone never answers the question the reader
/// actually has, which is where the ground was lost.
/// </summary>
public class PublicStandingRowDto
{
    public int Position { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int PlayedRounds { get; set; }
    public int AbsenceCount { get; set; }
    public int PenaltyPoints { get; set; }
    public bool IsEliminated { get; set; }

    /// <summary>Scored rounds only, oldest first. A locked round has no official result yet.</summary>
    public List<PublicStandingRoundDto> Rounds { get; set; } = new();
}

public class PublicStandingRoundDto
{
    public int Number { get; set; }
    public int Points { get; set; }
    public bool WasAbsent { get; set; }
    public bool FlavioRuleApplied { get; set; }
}

/// <summary>Base points per category, read from the season's ruleset (admins can edit them).</summary>
public class PublicRulesetDto
{
    public int ColumnOnly { get; set; }
    public int Traditional { get; set; }
    public int Medium { get; set; }
    public int Uncommon { get; set; }
    public int ExtraUncommon { get; set; }
}

/// <summary>
/// A round's full scoring breakdown. Reuses <see cref="RoundResultMatchDto"/> for the
/// fixtures — it already carries the audit context (effective multiplier, classic pair,
/// manual override) and nothing private.
/// </summary>
public class PublicRoundDto
{
    public int Number { get; set; }
    public string? Title { get; set; }
    public RoundStatus Status { get; set; }

    /// <summary>
    /// True when the round is locked but not yet scored: points are computed live from the
    /// results available so far, and absences, eliminations and the Flávio Rule do not apply.
    /// </summary>
    public bool IsPartial { get; set; }

    public int ComputedMatches { get; set; }
    public int RemainingMatches { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    public List<RoundResultMatchDto> Matches { get; set; } = new();
    public List<PublicParticipantScoreDto> Participants { get; set; } = new();
}

public class PublicParticipantScoreDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GrossPoints { get; set; }
    public int FinalPoints { get; set; }
    public int PenaltyPoints { get; set; }
    public bool WasAbsent { get; set; }
    public bool WasEliminated { get; set; }
    public bool FlavioRuleApplied { get; set; }
    public List<PublicMatchScoreDto> MatchScores { get; set; } = new();
}

/// <summary>
/// Per-match breakdown. Unlike the in-app <see cref="MatchScoreDto"/> this one carries the
/// predicted score: without it the page shows points with no way to check them, which is the
/// whole point of the link. Exposing it is gated on the round being closed — see the service.
/// </summary>
public class PublicMatchScoreDto
{
    public Guid RoundMatchId { get; set; }

    /// <summary>Null when the participant did not predict this match.</summary>
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }

    public int BasePoints { get; set; }
    public int Multiplier { get; set; }
    public int FinalPoints { get; set; }
    public ScoreCategory ScoreCategory { get; set; }
    public bool IsExactScore { get; set; }
    public bool IsCorrectColumn { get; set; }
}
