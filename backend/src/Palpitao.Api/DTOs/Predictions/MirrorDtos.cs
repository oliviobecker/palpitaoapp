using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Predictions;

public class MirrorMatchDto
{
    public Guid RoundMatchId { get; set; }
    public Competition Competition { get; set; }
    public MatchPhase Phase { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
}

public class MirrorPredictionDto
{
    public Guid RoundMatchId { get; set; }
    public int PredictedHomeScore { get; set; }
    public int PredictedAwayScore { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class MirrorParticipantDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAbsent { get; set; }
    public bool IsEliminated { get; set; }
    public bool FlavioRuleApplied { get; set; }
    public List<MirrorPredictionDto> Predictions { get; set; } = new();
}

public class MirrorDto
{
    public Guid RoundId { get; set; }
    public RoundStatus Status { get; set; }
    public List<MirrorMatchDto> Matches { get; set; } = new();
    public List<MirrorParticipantDto> Participants { get; set; } = new();
}
