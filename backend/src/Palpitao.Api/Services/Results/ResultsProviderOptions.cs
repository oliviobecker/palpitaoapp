namespace Palpitao.Api.Services.Results;

/// <summary>Bound from the "ResultsProvider" configuration section.</summary>
public class ResultsProviderOptions
{
    public const string SectionName = "ResultsProvider";

    /// <summary>"Manual" (default, no external fetch) or "ConfiguredWebsite".</summary>
    public string Provider { get; set; } = "Manual";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>When false, no external fetch happens; results come from manual entry.</summary>
    public bool Enabled { get; set; }

    public int TimeoutSeconds { get; set; } = 15;
}
