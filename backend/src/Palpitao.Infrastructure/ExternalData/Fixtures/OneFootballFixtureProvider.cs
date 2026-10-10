using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Application.Fixtures;
using Palpitao.Domain.Common;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.ExternalData.OneFootball;

namespace Palpitao.Infrastructure.ExternalData.Fixtures;

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

        OneFootballApi.Configure(_http, _options.OneFootballApiBaseUrl, _options.TimeoutSeconds);
    }

    public string SourceName => OneFootballApi.SourceName;

    public async Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Competition> competitions,
        CancellationToken cancellationToken)
    {
        var allowed = (competitions.Count > 0 ? competitions.Distinct() : OneFootballApi.Slugs.Keys).ToList();

        var result = new List<FixtureCandidateDto>();
        var seen = new HashSet<string>();
        var requested = 0;
        var failures = 0;

        foreach (var competition in allowed)
        {
            if (!OneFootballApi.Slugs.TryGetValue(competition, out var slug))
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

    private Task<JsonElement> FetchAsync(string path, CancellationToken ct)
        => OneFootballApi.FetchAsync(_http, path, "fixtures.fetchFailed", "fixtures", _logger, ct);

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
        OneFootballApi.VisitCards(root, IsMatchCard, cards.Add);

        foreach (var card in cards)
        {
            if (!TryParseDate(OneFootballApi.GetString(card, "kickoff"), out var startsAt) || startsAt < start || startsAt > end)
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
                : OneFootballApi.GetString(card, "link") ?? Guid.NewGuid().ToString("N");
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
            var value = OneFootballApi.GetString(card, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value!);
            }
        }

        return string.Join(" ", parts).ToLowerInvariant();
    }

    /// <summary>A fixture card: a match card with a string <c>kickoff</c>.</summary>
    private static bool IsMatchCard(JsonElement o)
        => o.TryGetProperty("kickoff", out var k) && k.ValueKind == JsonValueKind.String
            && OneFootballApi.HasHomeAndAwayNames(o);

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
