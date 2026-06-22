using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Fixtures;

/// <summary>
/// Fixture provider backed by TheSportsDB (v1 JSON API). Unlike API-Football's free
/// tier, the public test key returns <b>current-season</b> fixtures for all four
/// tracked competitions:
///   Premier League = 4328 · Championship = 4329 · League One = 4396 · FA Cup = 4482.
///
/// It calls <c>/{key}/eventsseason.php?id={league}&amp;s={YYYY-YYYY}</c> once per
/// competition and filters the season's events down to the requested period. The
/// integration is isolated here (no DB, no domain rules); any transport/parse error
/// raises <c>fixtures.fetchFailed</c> so the manual flow keeps working.
/// </summary>
public class TheSportsDbFixtureProvider : IFixtureProvider
{
    private static readonly IReadOnlyDictionary<Competition, int> LeagueIds = new Dictionary<Competition, int>
    {
        [Competition.PremierLeague] = 4328,
        [Competition.Championship] = 4329,
        [Competition.LeagueOne] = 4396,
        [Competition.FACup] = 4482,
    };

    private readonly HttpClient _http;
    private readonly FixtureOptions _options;
    private readonly ILogger<TheSportsDbFixtureProvider> _logger;

    public TheSportsDbFixtureProvider(
        HttpClient http,
        IOptions<FixtureOptions> options,
        ILogger<TheSportsDbFixtureProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        var baseUrl = string.IsNullOrWhiteSpace(_options.TheSportsDbBaseUrl)
            ? "https://www.thesportsdb.com/api/v1/json"
            : _options.TheSportsDbBaseUrl;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public string SourceName => "TheSportsDB";

    public async Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Competition> competitions,
        CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(_options.TheSportsDbKey) ? "3" : _options.TheSportsDbKey;
        var allowed = (competitions.Count > 0 ? competitions.Distinct() : LeagueIds.Keys).ToList();

        // A round window can straddle the season boundary (e.g. an FA Cup tie in
        // August belongs to the new season); query both seasons that touch the period.
        var seasons = SeasonsFor(startDate, endDate);

        var result = new List<FixtureCandidateDto>();
        var seen = new HashSet<string>();
        foreach (var competition in allowed)
        {
            if (!LeagueIds.TryGetValue(competition, out var leagueId))
            {
                continue; // not a tracked competition
            }

            foreach (var season in seasons)
            {
                var url = $"{key}/eventsseason.php?id={leagueId}&s={season}";
                var root = await FetchAsync(url, cancellationToken);
                ParseInto(result, seen, root, competition, startDate, endDate);
            }
        }

        return result;
    }

    /// <summary>
    /// TheSportsDB season labels are "YYYY-YYYY" (start year first). The English
    /// season runs Aug–May, so July+ belongs to that year's season. Returns the
    /// season(s) overlapping the requested window.
    /// </summary>
    private static IReadOnlyList<string> SeasonsFor(DateTime start, DateTime end)
    {
        var seasons = new List<string> { Season(start) };
        var endSeason = Season(end);
        if (endSeason != seasons[0])
        {
            seasons.Add(endSeason);
        }

        return seasons;

        static string Season(DateTime d)
        {
            var startYear = d.Month >= 7 ? d.Year : d.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }
    }

    private async Task<JsonElement> FetchAsync(string url, CancellationToken ct)
    {
        string payload;
        try
        {
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            payload = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogWarning(ex, "Failed to fetch fixtures from TheSportsDB ({Url}).", url);
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse TheSportsDB payload as JSON.");
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
        // No events for the league/season is a valid empty result ("events": null).
        if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in events.EnumerateArray())
        {
            var home = GetString(item, "strHomeTeam");
            var away = GetString(item, "strAwayTeam");
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            if (!TryResolveKickoff(item, out var startsAt) || startsAt < start || startsAt > end)
            {
                continue;
            }

            var id = GetString(item, "idEvent") ?? Guid.NewGuid().ToString("N");
            if (!seen.Add($"thesportsdb-{id}"))
            {
                continue; // same event returned by two overlapping seasons
            }

            acc.Add(new FixtureCandidateDto
            {
                ExternalId = $"thesportsdb-{id}",
                Competition = competition,
                Phase = MatchPhase.Regular,
                HomeTeamName = home!.Trim(),
                AwayTeamName = away!.Trim(),
                StartsAt = startsAt,
                Source = SourceName,
            });
        }
    }

    /// <summary>
    /// Uses <c>strTimestamp</c> (UTC) when present, otherwise combines
    /// <c>dateEvent</c> + <c>strTime</c>. Returns false when no usable kickoff exists.
    /// </summary>
    private static bool TryResolveKickoff(JsonElement item, out DateTime value)
    {
        var timestamp = GetString(item, "strTimestamp");
        if (!string.IsNullOrWhiteSpace(timestamp) && TryParseUtc(timestamp, out value))
        {
            return true;
        }

        var date = GetString(item, "dateEvent");
        var time = GetString(item, "strTime");
        if (!string.IsNullOrWhiteSpace(date))
        {
            var combined = string.IsNullOrWhiteSpace(time) ? $"{date}T00:00:00" : $"{date}T{time}";
            if (TryParseUtc(combined, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryParseUtc(string raw, out DateTime value)
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

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
