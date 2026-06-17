namespace Palpitao.Api.Services.Fixtures;

/// <summary>
/// Bound from the "Fixtures" configuration section (see appsettings.json /
/// .env.example). Controls the external fixture provider.
/// </summary>
public class FixtureOptions
{
    public const string SectionName = "Fixtures";

    /// <summary>
    /// Provider key: "OneFootball" (free, all four competitions, default),
    /// "FixtureDownload" (free, PL + Championship only), "ApiFootball" (all four but
    /// paid for the current season) or "TheSportsDb" (free key returns only samples).
    /// </summary>
    public string Provider { get; set; } = "OneFootball";

    /// <summary>Base URL of the OneFootball web-experience competition API.</summary>
    public string OneFootballApiBaseUrl { get; set; } =
        "https://api.onefootball.com/web-experience/en/competition";

    /// <summary>Base URL of the fixturedownload.com JSON feed.</summary>
    public string FixtureDownloadBaseUrl { get; set; } = "https://fixturedownload.com/feed/json";

    /// <summary>Base URL used by the OneFootball best-effort provider.</summary>
    public string BaseUrl { get; set; } = "https://onefootball.com";

    /// <summary>Base URL of the API-Football v3 endpoint (api-sports.io direct).</summary>
    public string ApiFootballBaseUrl { get; set; } = "https://v3.football.api-sports.io";

    /// <summary>
    /// API-Football key (<c>x-apisports-key</c>). Prefer overriding via the
    /// <c>Fixtures__ApiKey</c> environment variable / user secrets.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL of the TheSportsDB v1 JSON API.</summary>
    public string TheSportsDbBaseUrl { get; set; } = "https://www.thesportsdb.com/api/v1/json";

    /// <summary>TheSportsDB API key (path segment). "3" is the free/public test key.</summary>
    public string TheSportsDbKey { get; set; } = "3";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// When false, the search endpoint is disabled and the UI keeps only the
    /// manual flow. Lets operators turn off the best-effort scraping at runtime.
    /// </summary>
    public bool EnableExternalFixtureImport { get; set; } = true;
}
