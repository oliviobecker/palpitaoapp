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
/// scores and status, keyed by the <c>onefootball-{matchId}</c> external id (falling back
/// to team names — the id is only stored on a match once a refresh has matched it).
/// Neither tab is authoritative: "results" also lists not-yet-played fixtures, so the two
/// are merged and the best-informed card wins. One GET per tab, clear user-agent, timeout —
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

        // Both tabs list the same match — and "results" also carries not-yet-played cards — so we
        // merge on the match key and keep the best-informed card instead of the first one read.
        var byKey = new Dictionary<string, ExternalMatchResultDto>(StringComparer.OrdinalIgnoreCase);
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
                    ParseInto(byKey, root, competition);
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

        return byKey.Values.ToList();
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

    private void ParseInto(
        Dictionary<string, ExternalMatchResultDto> acc, JsonElement root, Competition competition)
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
            var key = externalId ?? $"{competition}|{home}|{away}";

            var (homeScore, awayScore) = ReadScores(card);
            var candidate = new ExternalMatchResultDto
            {
                ExternalMatchId = externalId,
                ExternalMatchUrl = GetString(card, "link"),
                Competition = competition,
                HomeTeamName = home!.Trim(),
                AwayTeamName = away!.Trim(),
                HomeScore = homeScore,
                AwayScore = awayScore,
                Status = ReadStatus(card, homeScore, awayScore),
            };

            if (!acc.TryGetValue(key, out var existing) || IsBetterInformed(candidate, existing))
            {
                acc[key] = candidate;
            }
        }
    }

    /// <summary>
    /// Which of two cards for the same match to keep. The tabs overlap and a competition page can
    /// also embed a thin "next match" card, so the one that knows the most about the game wins —
    /// whichever order we happened to read them in.
    /// </summary>
    private static bool IsBetterInformed(ExternalMatchResultDto candidate, ExternalMatchResultDto existing)
    {
        var rank = Rank(candidate);
        var current = Rank(existing);
        return rank != current
            ? rank > current
            : HasScores(candidate) && !HasScores(existing);
    }

    private static int Rank(ExternalMatchResultDto result) => result.Status switch
    {
        MatchStatus.Finished => 4,
        MatchStatus.InProgress => 3,
        MatchStatus.Postponed or MatchStatus.Cancelled => 2,
        _ => 1,
    };

    private static bool HasScores(ExternalMatchResultDto result)
        => result.HomeScore is not null && result.AwayScore is not null;

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

    /// <summary>
    /// Reads the card's state. <c>period</c> ("PRE_MATCH", "SECOND_HALF", "FULL_TIME") is the only
    /// field the web-experience cards actually carry; the others are kept as a defence against
    /// other OneFootball shapes. <c>timePeriod</c> holds the running clock ("66'") while the match
    /// is being played, so it settles a label the table does not know yet.
    /// </summary>
    private MatchStatus ReadStatus(JsonElement card, int? homeScore, int? awayScore)
    {
        var raw = GetString(card, "period")
            ?? GetString(card, "status")
            ?? GetString(card, "matchStatus")
            ?? GetString(card, "state");

        var status = MatchStatusParser.Parse(
            raw, homeScore, awayScore, GetString(card, "timePeriod"), out var unknownLabel);

        if (unknownLabel)
        {
            _logger.LogWarning(
                "Unknown OneFootball match state {State}; read as {Status}.", raw, status);
        }

        return status;
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
