using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Fixtures;

/// <summary>
/// Fixture provider backed by OneFootball's public web-experience API
/// (<c>api.onefootball.com/web-experience/en/competition/{slug}/fixtures</c>). Unlike
/// the free football APIs, OneFootball covers all four tracked competitions with the
/// <b>current season</b>:
///   Premier League · Championship · League One · FA Cup.
///
/// It issues one GET per competition with a clear user-agent and a timeout — no login,
/// no token, no bypass. The response is a nested "containers" document; match cards are
/// found by walking the tree for objects that carry <c>kickoff</c> + <c>homeTeam.name</c>
/// + <c>awayTeam.name</c>, which is filtered down to the requested period. If the
/// structure changes or every competition fails, it raises <c>fixtures.fetchFailed</c>
/// so the manual flow keeps working.
/// </summary>
public class OneFootballFixtureProvider : IFixtureProvider
{
    private static readonly IReadOnlyDictionary<Competition, string> Slugs = new Dictionary<Competition, string>
    {
        [Competition.PremierLeague] = "premier-league-9",
        [Competition.Championship] = "efl-championship-27",
        [Competition.LeagueOne] = "efl-league-one-42",
        [Competition.FACup] = "fa-cup-17",
        // https://onefootball.com/en/competition/fifa-world-cup-12/fixtures
        [Competition.FifaWorldCup] = "fifa-world-cup-12",
    };

    private readonly HttpClient _http;
    private readonly FixtureOptions _options;
    private readonly ILogger<OneFootballFixtureProvider> _logger;

    public OneFootballFixtureProvider(
        HttpClient http,
        IOptions<FixtureOptions> options,
        ILogger<OneFootballFixtureProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        var baseUrl = string.IsNullOrWhiteSpace(_options.OneFootballApiBaseUrl)
            ? "https://api.onefootball.com/web-experience/en/competition"
            : _options.OneFootballApiBaseUrl;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalpitaoEngland", "1.0"));
        }
    }

    public string SourceName => "OneFootball";

    public async Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Competition> competitions,
        CancellationToken cancellationToken)
    {
        var allowed = (competitions.Count > 0 ? competitions.Distinct() : Slugs.Keys).ToList();

        var result = new List<FixtureCandidateDto>();
        var seen = new HashSet<string>();
        var requested = 0;
        var failures = 0;

        foreach (var competition in allowed)
        {
            if (!Slugs.TryGetValue(competition, out var slug))
            {
                continue;
            }

            requested++;
            try
            {
                // The "fixtures" tab carries upcoming matches; off-season it is empty.
                var root = await FetchAsync($"{slug}/fixtures", cancellationToken);
                ParseInto(result, seen, root, competition, startDate, endDate);
            }
            catch (BusinessRuleException)
            {
                // Keep going so one slow/broken competition doesn't sink the whole search.
                failures++;
            }
        }

        // Only surface an error when every requested competition failed.
        if (requested > 0 && failures == requested)
        {
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        return result;
    }

    private async Task<JsonElement> FetchAsync(string path, CancellationToken ct)
    {
        string payload;
        try
        {
            using var response = await _http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default; // competition not available -> empty
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

            _logger.LogWarning(ex, "Failed to fetch fixtures from OneFootball ({Path}).", path);
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse OneFootball payload as JSON.");
            throw new BusinessRuleException("fixtures.fetchFailed");
        }
    }

    private void ParseInto(
        List<FixtureCandidateDto> acc,
        HashSet<string> seen,
        JsonElement root,
        Competition competition,
        DateTime start,
        DateTime end)
    {
        if (root.ValueKind != JsonValueKind.Object && root.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var cards = new List<JsonElement>();
        CollectMatchCards(root, cards);

        foreach (var card in cards)
        {
            if (!TryParseDate(GetString(card, "kickoff"), out var startsAt) || startsAt < start || startsAt > end)
            {
                continue;
            }

            var home = card.GetProperty("homeTeam").GetProperty("name").GetString();
            var away = card.GetProperty("awayTeam").GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            var matchId = card.TryGetProperty("matchId", out var mid)
                ? mid.ToString()
                : GetString(card, "link") ?? Guid.NewGuid().ToString("N");
            var id = $"onefootball-{matchId}";
            if (!seen.Add(id))
            {
                continue;
            }

            acc.Add(new FixtureCandidateDto
            {
                ExternalId = id,
                Competition = competition,
                Phase = ResolvePhase(card, competition),
                HomeTeamName = home!.Trim(),
                AwayTeamName = away!.Trim(),
                StartsAt = startsAt,
                Source = SourceName,
            });
        }
    }

    /// <summary>
    /// Recursively collects match-card objects: any object carrying a string
    /// <c>kickoff</c> and <c>homeTeam.name</c> / <c>awayTeam.name</c>.
    /// </summary>
    private static void CollectMatchCards(JsonElement node, List<JsonElement> cards)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsMatchCard(node))
                {
                    cards.Add(node);
                }

                foreach (var prop in node.EnumerateObject())
                {
                    CollectMatchCards(prop.Value, cards);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    CollectMatchCards(item, cards);
                }

                break;
        }
    }

    /// <summary>
    /// Maps a fixture card to its phase. England competitions stay
    /// <see cref="MatchPhase.Regular"/> (phase is implied by the competition). For the
    /// World Cup, the stage is inferred from any stage/round text on the card
    /// (e.g. "Round of 16", "Quarter-final", "Final"); when absent it defaults to the
    /// group stage and the admin can adjust it before importing.
    /// </summary>
    private static MatchPhase ResolvePhase(JsonElement card, Competition competition)
    {
        if (competition != Competition.FifaWorldCup)
        {
            return MatchPhase.Regular;
        }

        var text = CollectStageText(card);
        if (text.Contains("third") || text.Contains("3rd"))
        {
            return MatchPhase.WorldCupThirdPlace;
        }
        if (text.Contains("semi"))
        {
            return MatchPhase.WorldCupSemiFinal;
        }
        if (text.Contains("quarter"))
        {
            return MatchPhase.WorldCupQuarterFinal;
        }
        if (text.Contains("round of 16") || text.Contains("last 16"))
        {
            return MatchPhase.WorldCupRoundOf16;
        }
        if (text.Contains("round of 32") || text.Contains("last 32"))
        {
            return MatchPhase.WorldCupRoundOf32;
        }
        if (text.Contains("final"))
        {
            return MatchPhase.WorldCupFinal;
        }

        return MatchPhase.WorldCupGroupStage;
    }

    /// <summary>
    /// Lower-cased concatenation of the stage/round-ish string fields on the card,
    /// used to infer the World Cup phase. Reads only known stage keys (not team
    /// names) to avoid false positives.
    /// </summary>
    private static string CollectStageText(JsonElement card)
    {
        if (card.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        string[] keys =
        {
            "round", "roundName", "stage", "competitionStage", "matchdayName",
            "matchday", "section", "sectionHeader", "subtitle", "name", "title",
        };

        var parts = new List<string>();
        foreach (var key in keys)
        {
            var value = GetString(card, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value!);
            }
        }

        return string.Join(" ", parts).ToLowerInvariant();
    }

    private static bool IsMatchCard(JsonElement o)
        => o.TryGetProperty("kickoff", out var k) && k.ValueKind == JsonValueKind.String
            && o.TryGetProperty("homeTeam", out var h) && HasName(h)
            && o.TryGetProperty("awayTeam", out var a) && HasName(a);

    private static bool HasName(JsonElement team)
        => team.ValueKind == JsonValueKind.Object
            && team.TryGetProperty("name", out var n)
            && n.ValueKind == JsonValueKind.String;

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool TryParseDate(string? raw, out DateTime value)
    {
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out value))
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return true;
        }

        value = default;
        return false;
    }
}
