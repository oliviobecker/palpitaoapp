using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Scoring;

public class MatchResultRequest
{
    public int HomeScore { get; set; }

    public int AwayScore { get; set; }
}

public class StandingDto
{
    public int Position { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int PlayedRounds { get; set; }
    public int AbsenceCount { get; set; }
    public int PenaltyPoints { get; set; }
    public bool IsEliminated { get; set; }
}

public class MatchScoreDto
{
    public Guid RoundMatchId { get; set; }
    public int BasePoints { get; set; }
    public int Multiplier { get; set; }
    public int FinalPoints { get; set; }
    public ScoreCategory ScoreCategory { get; set; }
    public bool IsExactScore { get; set; }
    public bool IsCorrectColumn { get; set; }
}

public class RoundResultMatchDto
{
    public Guid RoundMatchId { get; set; }
    public Competition Competition { get; set; }
    public MatchPhase Phase { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public bool IsFinished { get; set; }

    /// <summary>Effective points multiplier of the match (manual override or the rule-based value).</summary>
    public int Multiplier { get; set; }

    /// <summary>True when both teams are classic-eligible (drives the audit "classic" badge).</summary>
    public bool IsClassic { get; set; }

    /// <summary>True when an admin manual override set the multiplier (audit context).</summary>
    public bool IsManualMultiplier { get; set; }
}

public class RoundResultParticipantDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GrossPoints { get; set; }
    public int FinalPoints { get; set; }
    public int PenaltyPoints { get; set; }
    public bool WasAbsent { get; set; }
    public bool WasEliminated { get; set; }
    public bool FlavioRuleApplied { get; set; }
    public List<MatchScoreDto> MatchScores { get; set; } = new();
}

public class RoundResultsDto
{
    public Guid RoundId { get; set; }
    public RoundStatus Status { get; set; }
    public List<RoundResultMatchDto> Matches { get; set; } = new();
    public List<RoundResultParticipantDto> Participants { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Scoring configuration (per-season, admin-editable ruleset)
// ---------------------------------------------------------------------------

/// <summary>Base points per category (before the multiplier).</summary>
public class ScoringBasePointsDto
{
    public int ColumnOnly { get; set; }
    public int Traditional { get; set; }
    public int Medium { get; set; }
    public int Uncommon { get; set; }
    public int ExtraUncommon { get; set; }
}

/// <summary>One exact score (normalized min,max) mapped to a difficulty category.</summary>
public class ScoringScoreEntryDto
{
    public int Low { get; set; }
    public int High { get; set; }
    public ScoreCategory Category { get; set; }
}

/// <summary>Multiplier of a (competition, phase): normal value and classic value.</summary>
public class ScoringMultiplierRuleDto
{
    public Competition Competition { get; set; }
    public MatchPhase Phase { get; set; }
    public int Multiplier { get; set; }
    public int ClassicMultiplier { get; set; }
}

/// <summary>A candidate team for classic designation, with its current selection state.</summary>
public class ScoringConfigTeamDto
{
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public bool IsClassic { get; set; }
}

/// <summary>The full editable scoring ruleset of a season (response).</summary>
public class ScoringConfigDto
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public TournamentType TournamentType { get; set; }

    /// <summary>True when the season already has scored rounds — edits need a recalculate to take effect.</summary>
    public bool HasScoredRounds { get; set; }

    public ScoringBasePointsDto BasePoints { get; set; } = new();
    public List<ScoringScoreEntryDto> ScoreEntries { get; set; } = new();
    public List<ScoringMultiplierRuleDto> MultiplierRules { get; set; } = new();

    /// <summary>Candidate classic teams for the season's tournament type, with selection.</summary>
    public List<ScoringConfigTeamDto> Teams { get; set; } = new();
}

/// <summary>The editable scoring ruleset of a season (request).</summary>
public class ScoringConfigRequest
{
    public ScoringBasePointsDto BasePoints { get; set; } = new();
    public List<ScoringScoreEntryDto> ScoreEntries { get; set; } = new();
    public List<ScoringMultiplierRuleDto> MultiplierRules { get; set; } = new();
    public List<Guid> ClassicTeamIds { get; set; } = new();
}
