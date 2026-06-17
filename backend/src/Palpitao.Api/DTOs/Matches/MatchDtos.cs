using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Matches;

public class CreateMatchRequest
{
    public Competition? Competition { get; set; }

    public MatchPhase? Phase { get; set; }

    public Guid HomeTeamId { get; set; }

    public Guid AwayTeamId { get; set; }

    public DateTime? StartsAt { get; set; }

    public int Order { get; set; }

    public int? ManualMultiplierOverride { get; set; }

    public string? ManualMultiplierJustification { get; set; }

    /// <summary>Required to edit matches of a Locked/Scored/Cancelled round.</summary>
    public string? OverrideLockJustification { get; set; }
}

public class UpdateMatchRequest : CreateMatchRequest
{
}

public class MatchDto
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Competition Competition { get; set; }
    public MatchPhase Phase { get; set; }
    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public int Order { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public bool IsFinished { get; set; }
    public int? ManualMultiplierOverride { get; set; }
    public string? ManualMultiplierJustification { get; set; }
}
