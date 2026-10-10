using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Palpitao.Domain.Common;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.ExternalData.OneFootball;

/// <summary>
/// What the OneFootball fixture, results and team-catalogue adapters share: the public
/// web-experience API (<c>.../competition/{slug}/{fixtures|results}</c>), its competition slugs,
/// the client setup (one GET per tab, a clear user-agent and a timeout — no login, no token),
/// the fetch with its failure mapping, and the walk that finds match cards in the nested
/// "containers" document.
/// </summary>
internal static class OneFootballApi
{
    public const string SourceName = "OneFootball";

    private const string DefaultBaseUrl = "https://api.onefootball.com/web-experience/en/competition";

    /// <summary>
    /// Every competition OneFootball serves, in the order the fixture search walks them when no
    /// competition is requested.
    /// </summary>
    public static readonly IReadOnlyDictionary<Competition, string> Slugs = new Dictionary<Competition, string>
    {
        [Competition.PremierLeague] = "premier-league-9",
        [Competition.Championship] = "efl-championship-27",
        [Competition.LeagueOne] = "efl-league-one-42",
        [Competition.FACup] = "fa-cup-17",
        // https://onefootball.com/en/competition/fifa-world-cup-12/fixtures
        [Competition.FifaWorldCup] = "fifa-world-cup-12",
    };

    /// <summary>
    /// The league divisions: cups draw from every division, so only a league's cards say which
    /// league a club belongs to.
    /// </summary>
    public static readonly IReadOnlySet<Competition> Leagues = new HashSet<Competition>
    {
        Competition.PremierLeague, Competition.Championship, Competition.LeagueOne,
    };

    /// <summary>Points the typed client at the API (or the configured override).</summary>
    public static void Configure(HttpClient http, string? baseUrl, int timeoutSeconds)
    {
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 60));
        var url = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
        http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        if (!http.DefaultRequestHeaders.UserAgent.Any())
        {
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalpitaoEngland", "1.0"));
        }
    }

    /// <summary>
    /// GETs and parses one tab. A 404 (competition not available) reads as an empty document;
    /// a transport failure or an unparseable body is logged and raised as
    /// <paramref name="errorKey"/>. A cancellation the caller asked for propagates as-is.
    /// </summary>
    /// <param name="resource">What is being fetched, for the log line (e.g. "fixtures").</param>
    public static async Task<JsonElement> FetchAsync(
        HttpClient http, string path, string errorKey, string resource, ILogger logger, CancellationToken ct)
    {
        string payload;
        try
        {
            using var response = await http.GetAsync(path, ct);
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

            logger.LogWarning(ex, "Failed to fetch {Resource} from OneFootball ({Path}).", resource, path);
            throw new BusinessRuleException(errorKey);
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse the OneFootball {Resource} payload as JSON.", resource);
            throw new BusinessRuleException(errorKey);
        }
    }

    /// <summary>
    /// Walks the whole document and hands every object that <paramref name="isCard"/> accepts
    /// to <paramref name="visit"/>, descending into cards as well (a card can embed others).
    /// </summary>
    public static void VisitCards(JsonElement node, Func<JsonElement, bool> isCard, Action<JsonElement> visit)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (isCard(node))
                {
                    visit(node);
                }

                foreach (var prop in node.EnumerateObject())
                {
                    VisitCards(prop.Value, isCard, visit);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    VisitCards(item, isCard, visit);
                }

                break;
        }
    }

    /// <summary>
    /// A match card: an object with <c>homeTeam.name</c> and <c>awayTeam.name</c>. A
    /// results-tab card carries no kickoff, so the names are the one marker every tab shares.
    /// </summary>
    public static bool HasHomeAndAwayNames(JsonElement o)
        => o.TryGetProperty("homeTeam", out var h) && HasName(h)
            && o.TryGetProperty("awayTeam", out var a) && HasName(a);

    public static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool HasName(JsonElement team)
        => team.ValueKind == JsonValueKind.Object
            && team.TryGetProperty("name", out var n)
            && n.ValueKind == JsonValueKind.String;
}
