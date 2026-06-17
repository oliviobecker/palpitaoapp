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
