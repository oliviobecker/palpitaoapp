using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Application.Fixtures;
using Palpitao.Application.Results;
using Palpitao.Domain.Common;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.ExternalData.OneFootball;

namespace Palpitao.Infrastructure.ExternalData.Results;

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

        OneFootballApi.Configure(_http, fixtureOptions.Value.OneFootballApiBaseUrl, _options.TimeoutSeconds);
    }

    public string Name => OneFootballApi.SourceName;

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
            if (!OneFootballApi.Slugs.TryGetValue(competition, out var slug))
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

    private Task<JsonElement> FetchAsync(string path, CancellationToken ct)
        => OneFootballApi.FetchAsync(_http, path, "results.fetchFailed", "results", _logger, ct);

    private void ParseInto(
        Dictionary<string, ExternalMatchResultDto> acc, JsonElement root, Competition competition)
    {
        if (root.ValueKind != JsonValueKind.Object && root.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var cards = new List<JsonElement>();
        OneFootballApi.VisitCards(root, OneFootballApi.HasHomeAndAwayNames, cards.Add);

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
                : OneFootballApi.GetString(card, "link");
            var externalId = matchId is null ? null : $"onefootball-{matchId}";
            var key = externalId ?? $"{competition}|{home}|{away}";

            var (homeScore, awayScore) = ReadScores(card);
            var candidate = new ExternalMatchResultDto
            {
                ExternalMatchId = externalId,
                ExternalMatchUrl = OneFootballApi.GetString(card, "link"),
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

        var line = OneFootballApi.GetString(card, "scoreLine") ?? OneFootballApi.GetString(card, "score");
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
        var raw = OneFootballApi.GetString(card, "period")
            ?? OneFootballApi.GetString(card, "status")
            ?? OneFootballApi.GetString(card, "matchStatus")
            ?? OneFootballApi.GetString(card, "state");

        var status = MatchStatusParser.Parse(
            raw, homeScore, awayScore, OneFootballApi.GetString(card, "timePeriod"), out var unknownLabel);

        if (unknownLabel)
        {
            _logger.LogWarning(
                "Unknown OneFootball match state {State}; read as {Status}.", raw, status);
        }

        return status;
    }

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
