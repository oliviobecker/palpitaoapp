using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;

namespace Palpitao.Api.Services.Teams;

/// <summary>
/// Derives a competition's squad list from the same OneFootball web-experience API
/// used to import fixtures and results. There is no roster endpoint, so the list is
/// the union of the team names on both tabs: <c>{slug}/fixtures</c> (upcoming) and
/// <c>{slug}/results</c> (already played). Mid-season that union covers the whole
/// division; off-season both tabs can be thin or empty, which is why the sync
/// reports how many names it found instead of assuming full coverage.
/// </summary>
/// <remarks>
/// Only league divisions are mapped: cups draw from every division, so their cards
/// say nothing about which league a club belongs to.
/// </remarks>
public class OneFootballTeamCatalogProvider : ITeamCatalogProvider
{
    private static readonly IReadOnlyDictionary<Competition, string> Slugs = new Dictionary<Competition, string>
    {
        [Competition.PremierLeague] = "premier-league-9",
        [Competition.Championship] = "efl-championship-27",
        [Competition.LeagueOne] = "efl-league-one-42",
    };

    private readonly HttpClient _http;
    private readonly ILogger<OneFootballTeamCatalogProvider> _logger;

    public OneFootballTeamCatalogProvider(
        HttpClient http,
        IOptions<FixtureOptions> fixtureOptions,
        ILogger<OneFootballTeamCatalogProvider> logger)
    {
        _http = http;
        _logger = logger;

        var options = fixtureOptions.Value;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 60));
        var baseUrl = string.IsNullOrWhiteSpace(options.OneFootballApiBaseUrl)
            ? "https://api.onefootball.com/web-experience/en/competition"
            : options.OneFootballApiBaseUrl;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalpitaoEngland", "1.0"));
        }
    }

    public string SourceName => "OneFootball";

    public async Task<IReadOnlyList<string>> GetTeamNamesAsync(Competition competition, CancellationToken cancellationToken)
    {
        if (!Slugs.TryGetValue(competition, out var slug))
        {
            return Array.Empty<string>();
        }

        // Keyed by the normalized name so the two tabs (and any spelling drift within
        // a tab) collapse to one entry; the first spelling seen is the one reported.
        var names = new Dictionary<string, string>();
        foreach (var tab in new[] { "fixtures", "results" })
        {
            var root = await FetchAsync($"{slug}/{tab}", cancellationToken);
            CollectNames(root, names);
        }

        return names.Values.ToList();
    }

    private async Task<JsonElement> FetchAsync(string path, CancellationToken ct)
    {
        string payload;
        try
        {
            using var response = await _http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            response.EnsureSuccessStatusCode();
            payload = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            // Unlike the results refresh, a partial answer here is worse than none:
            // every club missing from a half-loaded roster would be reported as
            // "not found in the source", so a failed tab fails the whole sync.
            _logger.LogWarning(ex, "Failed to fetch the team catalogue from OneFootball ({Path}).", path);
            throw new BusinessRuleException("teams.syncFailed");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse the OneFootball team catalogue payload as JSON.");
            throw new BusinessRuleException("teams.syncFailed");
        }
    }

    private static void CollectNames(JsonElement node, Dictionary<string, string> names)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsMatchCard(node))
                {
                    AddName(node, "homeTeam", names);
                    AddName(node, "awayTeam", names);
                }

                foreach (var prop in node.EnumerateObject())
                {
                    CollectNames(prop.Value, names);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    CollectNames(item, names);
                }

                break;
        }
    }

    private static void AddName(JsonElement card, string teamProperty, Dictionary<string, string> names)
    {
        var name = card.GetProperty(teamProperty).GetProperty("name").GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();
        names.TryAdd(FootballReference.Normalize(trimmed), trimmed);
    }

    // --- JSON walking (mirrors the OneFootball results provider) --------------
    // A results-tab card carries no kickoff, so the home/away names are the only
    // reliable marker of a match card here.

    private static bool IsMatchCard(JsonElement o)
        => o.TryGetProperty("homeTeam", out var h) && HasName(h)
            && o.TryGetProperty("awayTeam", out var a) && HasName(a);

    private static bool HasName(JsonElement team)
        => team.ValueKind == JsonValueKind.Object
            && team.TryGetProperty("name", out var n)
            && n.ValueKind == JsonValueKind.String;
}
