using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Application.Fixtures;
using Palpitao.Application.Teams;
using Palpitao.Domain.Common;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.ExternalData.OneFootball;

namespace Palpitao.Infrastructure.ExternalData.Teams;

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
        OneFootballApi.Configure(_http, options.OneFootballApiBaseUrl, options.TimeoutSeconds);
    }

    public string SourceName => OneFootballApi.SourceName;

    public async Task<IReadOnlyList<string>> GetTeamNamesAsync(Competition competition, CancellationToken cancellationToken)
    {
        if (!OneFootballApi.Leagues.Contains(competition)
            || !OneFootballApi.Slugs.TryGetValue(competition, out var slug))
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

    // Unlike the results refresh, a partial answer here is worse than none: every club missing
    // from a half-loaded roster would be reported as "not found in the source", so a failed tab
    // fails the whole sync.
    private Task<JsonElement> FetchAsync(string path, CancellationToken ct)
        => OneFootballApi.FetchAsync(_http, path, "teams.syncFailed", "team catalogue", _logger, ct);

    private static void CollectNames(JsonElement root, Dictionary<string, string> names)
        => OneFootballApi.VisitCards(root, OneFootballApi.HasHomeAndAwayNames, card =>
        {
            AddName(card, "homeTeam", names);
            AddName(card, "awayTeam", names);
        });

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
}
