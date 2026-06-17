using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Results provider backed by the same OneFootball web-experience API used to import
/// fixtures (<c>.../competition/{slug}/fixtures</c> and <c>.../results</c>). For each
/// competition present in the round it fetches the cards and reads the live/final
/// scores and status, keyed by the <c>onefootball-{matchId}</c> external id stored at
/// import (falling back to team names). One GET per tab, clear user-agent, timeout —
/// no login, no token. Enabled when <c>ResultsProvider:Provider = "OneFootball"</c> and
/// <c>Enabled = true</c>; otherwise the manual flow keeps working.
/// </summary>
public class OneFootballResultsProvider : IResultsProvider
{
    private static readonly IReadOnlyDictionary<Competition, string> Slugs = new Dictionary<Competition, string>
    {
        [Competition.PremierLeague] = "premier-league-9",
        [Competition.Championship] = "efl-championship-27",
        [Competition.LeagueOne] = "efl-league-one-42",
        [Competition.FACup] = "fa-cup-17",
        [Competition.FifaWorldCup] = "fifa-world-cup-12",
    };

    private readonly HttpClient _http;
    private readonly ResultsProviderOptions _options;
    private readonly ILogger<OneFootballResultsProvider> _logger;

    public OneFootballResultsProvider(
        HttpClient http,
        IOptions<ResultsProviderOptions> options,
        IOptions<FixtureOptions> fixtureOptions,
        ILogger<OneFootballResultsProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        var baseUrl = string.IsNullOrWhiteSpace(fixtureOptions.Value.OneFootballApiBaseUrl)
            ? "https://api.onefootball.com/web-experience/en/competition"
            : fixtureOptions.Value.OneFootballApiBaseUrl;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalpitaoEngland", "1.0"));
        }
    }

    public string Name => "OneFootball";

    public bool IsEnabled => _options.Enabled;

    public async Task<IReadOnlyList<ExternalMatchResultDto>> GetResultsForRoundAsync(
        Round round, CancellationToken cancellationToken)
    {
        var competitions = round.Matches.Select(m => m.Competition).Distinct().ToList();

        var results = new List<ExternalMatchResultDto>();
        var seen = new HashSet<string>();
        var requested = 0;
        var failures = 0;

        foreach (var competition in competitions)
        {
            if (!Slugs.TryGetValue(competition, out var slug))
            {
                continue;
            }

            // Upcoming/live matches live on "fixtures"; played matches on "results".
            foreach (var tab in new[] { "fixtures", "results" })
            {
                requested++;
                try
                {
                    var root = await FetchAsync($"{slug}/{tab}", cancellationToken);
                    ParseInto(results, seen, root);
                }
                catch (BusinessRuleException)
                {
                    failures++;
                }
            }
        }

        if (requested > 0 && failures == requested)
        {
            throw new BusinessRuleException("results.fetchFailed");
        }

        return results;
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

            _logger.LogWarning(ex, "Failed to fetch results from OneFootball ({Path}).", path);
            throw new BusinessRuleException("results.fetchFailed");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse OneFootball payload as JSON.");
            throw new BusinessRuleException("results.fetchFailed");
        }
    }

    private static void ParseInto(List<ExternalMatchResultDto> acc, HashSet<string> seen, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object && root.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var cards = new List<JsonElement>();
        CollectMatchCards(root, cards);

        foreach (var card in cards)
        {
            var home = card.GetProperty("homeTeam").GetProperty("name").GetString();
            var away = card.GetProperty("awayTeam").GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            var matchId = card.TryGetProperty("matchId", out var mid)
                ? mid.ToString()
                : GetString(card, "link");
            var externalId = matchId is null ? null : $"onefootball-{matchId}";
            // Dedupe on whatever key we have so the two tabs don't double-report.
            var dedupe = externalId ?? $"{home}|{away}";
            if (!seen.Add(dedupe))
            {
                continue;
            }

            var (homeScore, awayScore) = ReadScores(card);
            acc.Add(new ExternalMatchResultDto
            {
                ExternalMatchId = externalId,
                ExternalMatchUrl = GetString(card, "link"),
                HomeTeamName = home!.Trim(),
                AwayTeamName = away!.Trim(),
                HomeScore = homeScore,
                AwayScore = awayScore,
                Status = ReadStatus(card, homeScore, awayScore),
            });
        }
    }

    /// <summary>Reads the home/away scores from the common OneFootball shapes:
    /// flat <c>homeScore</c>/<c>awayScore</c>, nested <c>homeTeam.score</c>, or a
    /// <c>"1:0"</c>/<c>"1-0"</c> score line.</summary>
    private static (int? Home, int? Away) ReadScores(JsonElement card)
    {
        var home = GetInt(card, "homeScore") ?? GetTeamScore(card, "homeTeam");
        var away = GetInt(card, "awayScore") ?? GetTeamScore(card, "awayTeam");
        if (home is not null || away is not null)
        {
            return (home, away);
        }

        var line = GetString(card, "scoreLine") ?? GetString(card, "score");
        if (!string.IsNullOrWhiteSpace(line))
        {
            var parts = line.Split(':', '-');
            if (parts.Length == 2
                && int.TryParse(parts[0].Trim(), out var h)
                && int.TryParse(parts[1].Trim(), out var a))
            {
                return (h, a);
            }
        }

        return (null, null);
    }

    private static int? GetTeamScore(JsonElement card, string teamProperty)
        => card.TryGetProperty(teamProperty, out var team) && team.ValueKind == JsonValueKind.Object
            ? GetInt(team, "score")
            : null;

    private static MatchStatus ReadStatus(JsonElement card, int? homeScore, int? awayScore)
    {
        var raw = (GetString(card, "status")
            ?? GetString(card, "matchStatus")
            ?? GetString(card, "period")
            ?? GetString(card, "state")
            ?? string.Empty).Trim().ToLowerInvariant();

        return raw switch
        {
            "inprogress" or "in_progress" or "live" or "playing" or "1h" or "2h" or "ht" => MatchStatus.InProgress,
            "finished" or "ft" or "fulltime" or "full_time" or "ended" or "aet" or "pen" => MatchStatus.Finished,
            "postponed" or "susp" or "suspended" => MatchStatus.Postponed,
            "cancelled" or "canceled" or "abandoned" => MatchStatus.Cancelled,
            "" when homeScore is not null && awayScore is not null => MatchStatus.Finished,
            _ => MatchStatus.NotStarted,
        };
    }

    // --- JSON walking (mirrors the OneFootball fixture provider) -------------

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

    private static bool IsMatchCard(JsonElement o)
        => o.TryGetProperty("homeTeam", out var h) && HasName(h)
            && o.TryGetProperty("awayTeam", out var a) && HasName(a);

    private static bool HasName(JsonElement team)
        => team.ValueKind == JsonValueKind.Object
            && team.TryGetProperty("name", out var n)
            && n.ValueKind == JsonValueKind.String;

    private static string? GetString(JsonElement e, string property)
        => e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement e, string property)
    {
        if (!e.TryGetProperty(property, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }
}
