using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

public class RoundMatch
{
    public Guid Id { get; set; }

    public Guid RoundId { get; set; }

    public Competition Competition { get; set; }

    public MatchPhase Phase { get; set; } = MatchPhase.Regular;

    public Guid HomeTeamId { get; set; }

    public Guid AwayTeamId { get; set; }

    public DateTime StartsAt { get; set; }

    /// <summary>Display order of the match within the round.</summary>
    public int Order { get; set; }

    // Final result (null until the match is finished / result is entered).
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public bool IsFinished { get; set; }

    /// <summary>Live status (NotStarted/InProgress/Finished/Postponed/Cancelled).</summary>
    public MatchStatus Status { get; set; } = MatchStatus.NotStarted;

    /// <summary>Where the current result came from (e.g. "Manual", "ConfiguredWebsite").</summary>
    public string? ResultSource { get; set; }

    /// <summary>Identifier of the match on the external results source, when known.</summary>
    public string? ExternalMatchId { get; set; }

    /// <summary>URL of the match on the external results source, when known.</summary>
    public string? ExternalMatchUrl { get; set; }

    /// <summary>Timestamp of the last result update (manual or via refresh).</summary>
    public DateTime? LastResultUpdatedAt { get; set; }

    /// <summary>
    /// Optional manual override of the computed multiplier. When set, requires a
    /// justification and bypasses the automatic rules.
    /// </summary>
    public int? ManualMultiplierOverride { get; set; }

    public string? ManualMultiplierJustification { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Round? Round { get; set; }
    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
