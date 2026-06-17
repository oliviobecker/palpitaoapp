using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Fixtures;

/// <summary>
/// Free fixture provider backed by fixturedownload.com. It serves the full
/// <b>current season</b> as a static JSON feed (no key) for the two English
/// competitions it publishes: Premier League (<c>epl</c>) and Championship
/// (<c>championship</c>).
///
/// League One and FA Cup are NOT available on any reliable free feed, so they are
/// not fetched here — the admin adds those manually (or switches to a paid
/// API-Football plan via <c>Fixtures:Provider=ApiFootball</c>).
///
/// One GET per competition (<c>/{slug}-{startYear}</c>) with a clear user-agent and
/// timeout; the season's matches are filtered down to the requested period. Any
/// transport/parse error raises <c>fixtures.fetchFailed</c> so the manual flow keeps
/// working.
/// </summary>
public class FixtureDownloadFixtureProvider : IFixtureProvider
{
    private static readonly IReadOnlyDictionary<Competition, string> Slugs = new Dictionary<Competition, string>
    {
        [Competition.PremierLeague] = "epl",
        [Competition.Championship] = "championship",
    };

    private readonly HttpClient _http;
    private readonly FixtureOptions _options;
    private readonly ILogger<FixtureDownloadFixtureProvider> _logger;

    public FixtureDownloadFixtureProvider(
        HttpClient http,
        IOptions<FixtureOptions> options,
        ILogger<FixtureDownloadFixtureProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        var baseUrl = string.IsNullOrWhiteSpace(_options.FixtureDownloadBaseUrl)
            ? "https://fixturedownload.com/feed/json"
            : _options.FixtureDownloadBaseUrl;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalpitaoEngland", "1.0"));
        }
    }

    public string SourceName => "FixtureDownload";

    public async Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Competition> competitions,
        CancellationToken cancellationToken)
    {
        var allowed = (competitions.Count > 0 ? competitions.Distinct() : Slugs.Keys).ToList();

        // English season label = start year; a window may straddle two seasons.
        var years = SeasonYears(startDate, endDate);

        var result = new List<FixtureCandidateDto>();
        var seen = new HashSet<string>();
        foreach (var competition in allowed)
        {
            if (!Slugs.TryGetValue(competition, out var slug))
            {
                continue; // not published on the free feed (e.g. League One, FA Cup)
            }

            foreach (var year in years)
            {
                var root = await FetchAsync($"{slug}-{year}", cancellationToken);
                ParseInto(result, seen, root, competition, slug, startDate, endDate);
            }
        }

        return result;
    }

    private static IReadOnlyList<int> SeasonYears(DateTime start, DateTime end)
    {
        var years = new List<int> { SeasonYear(start) };
        var endYear = SeasonYear(end);
        if (endYear != years[0])
        {
            years.Add(endYear);
        }

        return years;

        static int SeasonYear(DateTime d) => d.Month >= 7 ? d.Year : d.Year - 1;
    }

    private async Task<JsonElement> FetchAsync(string path, CancellationToken ct)
    {
        string payload;
        try
        {
            using var response = await _http.GetAsync(path, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No feed for that competition/season yet — treat as empty.
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

            _logger.LogWarning(ex, "Failed to fetch fixtures from fixturedownload ({Path}).", path);
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse fixturedownload payload as JSON.");
            throw new BusinessRuleException("fixtures.fetchFailed");
        }
    }

    private void ParseInto(
        List<FixtureCandidateDto> acc,
        HashSet<string> seen,
        JsonElement root,
        Competition competition,
        string slug,
        DateTime start,
        DateTime end)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return; // empty / 404
        }

        foreach (var item in root.EnumerateArray())
        {
            var home = GetString(item, "HomeTeam");
            var away = GetString(item, "AwayTeam");
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            if (!TryParseDate(GetString(item, "DateUtc"), out var startsAt) || startsAt < start || startsAt > end)
            {
                continue;
            }

            var matchNumber = item.TryGetProperty("MatchNumber", out var mn) && mn.ValueKind == JsonValueKind.Number
                ? mn.GetInt32().ToString(CultureInfo.InvariantCulture)
                : Guid.NewGuid().ToString("N");
            var id = $"fixturedownload-{slug}-{startsAt:yyyy}-{matchNumber}";
            if (!seen.Add(id))
            {
                continue;
            }

            acc.Add(new FixtureCandidateDto
            {
                ExternalId = id,
                Competition = competition,
                Phase = MatchPhase.Regular,
                HomeTeamName = home!.Trim(),
                AwayTeamName = away!.Trim(),
                StartsAt = startsAt,
                Source = SourceName,
            });
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Parses fixturedownload's "yyyy-MM-dd HH:mm:ssZ" timestamp (UTC).</summary>
    private static bool TryParseDate(string? raw, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Replace(' ', 'T');
        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out value))
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}
