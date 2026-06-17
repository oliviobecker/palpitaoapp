namespace Palpitao.Api.Services.Results;

/// <summary>Bound from the "ResultsRefresh" configuration section. Drives the
/// periodic background refresh of in-play rounds. Disabled by default.</summary>
public class ResultsRefreshOptions
{
    public const string SectionName = "ResultsRefresh";

    /// <summary>When false (default), the background refresh never runs.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often to refresh, in minutes (minimum 1).</summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>Reserved: only refresh active (Published/Locked) rounds. Always true today.</summary>
    public bool OnlyActiveRounds { get; set; } = true;
}
