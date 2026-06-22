using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Fixtures;

/// <summary>
/// Reliable fixture provider backed by API-Football (api-sports.io v3). Covers all
/// four tracked competitions:
///   Premier League = 39 · Championship = 40 · League One = 41 · FA Cup = 45.
///
/// One GET per requested competition (<c>/fixtures?league&amp;season&amp;from&amp;to</c>),
/// authenticated with <c>x-apisports-key</c>, with a hard timeout. The integration is
/// isolated here (no DB, no domain rules); on any transport/parse error or an
/// API-level <c>errors</c> payload it raises <c>fixtures.fetchFailed</c> so the admin
/// gets a clear message and the manual flow keeps working.
/// </summary>
public class ApiFootballFixtureProvider : IFixtureProvider
{
    private static readonly IReadOnlyDictionary<Competition, int> LeagueIds = new Dictionary<Competition, int>
    {
        [Competition.PremierLeague] = 39,
        [Competition.Championship] = 40,
        [Competition.LeagueOne] = 41,
        [Competition.FACup] = 45,
    };

    private readonly HttpClient _http;
    private readonly FixtureOptions _options;
    private readonly ILogger<ApiFootballFixtureProvider> _logger;

    public ApiFootballFixtureProvider(
        HttpClient http,
        IOptions<FixtureOptions> options,
        ILogger<ApiFootballFixtureProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        var baseUrl = string.IsNullOrWhiteSpace(_options.ApiFootballBaseUrl)
            ? "https://v3.football.api-sports.io"
            : _options.ApiFootballBaseUrl;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey) && !_http.DefaultRequestHeaders.Contains("x-apisports-key"))
        {
            _http.DefaultRequestHeaders.Add("x-apisports-key", _options.ApiKey);
        }
    }

    public string SourceName => "API-Football";

    public async Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Competition> competitions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("API-Football key is not configured (Fixtures:ApiKey).");
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        var allowed = (competitions.Count > 0 ? competitions.Distinct() : LeagueIds.Keys).ToList();
        var season = SeasonFor(startDate);
        var from = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var result = new List<FixtureCandidateDto>();
        foreach (var competition in allowed)
        {
            if (!LeagueIds.TryGetValue(competition, out var leagueId))
            {
                continue; // not a tracked competition
            }

            var url = $"fixtures?league={leagueId}&season={season}&from={from}&to={to}&timezone=UTC";
            var root = await FetchAsync(url, cancellationToken);
            ParseInto(result, root, competition, startDate, endDate);
        }

        return result;
    }

    /// <summary>
    /// English season label used by API-Football is the starting year (2025 for
    /// 2025/2026). The season runs Aug–May, so a July+ date belongs to that year.
    /// </summary>
    private static int SeasonFor(DateTime date) => date.Month >= 7 ? date.Year : date.Year - 1;

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

            _logger.LogWarning(ex, "Failed to fetch fixtures from API-Football ({Url}).", url);
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse API-Football payload as JSON.");
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        if (HasApiErrors(root))
        {
            _logger.LogWarning("API-Football returned errors: {Errors}",
                root.TryGetProperty("errors", out var e) ? e.ToString() : "(unknown)");
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        return root;
    }

    /// <summary>API-Football reports issues in an <c>errors</c> field that is an empty
    /// array when fine, or a populated object/array (bad key, quota, bad params).</summary>
    private static bool HasApiErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors))
        {
            return false;
        }

        return errors.ValueKind switch
        {
            JsonValueKind.Array => errors.GetArrayLength() > 0,
            JsonValueKind.Object => errors.EnumerateObject().Any(),
            _ => false,
        };
    }

    private void ParseInto(
        List<FixtureCandidateDto> acc,
        JsonElement root,
        Competition competition,
        DateTime start,
        DateTime end)
    {
        if (!root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("API-Football payload did not contain a 'response' array.");
            throw new BusinessRuleException("fixtures.fetchFailed");
        }

        foreach (var item in response.EnumerateArray())
        {
            if (!item.TryGetProperty("fixture", out var fixture)
                || !item.TryGetProperty("teams", out var teams)
                || !teams.TryGetProperty("home", out var homeTeam)
                || !teams.TryGetProperty("away", out var awayTeam))
            {
                continue;
            }

            if (!TryParseDate(GetString(fixture, "date"), out var startsAt) || startsAt < start || startsAt > end)
            {
                continue;
            }

            var home = GetString(homeTeam, "name");
            var away = GetString(awayTeam, "name");
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            var round = item.TryGetProperty("league", out var league) ? GetString(league, "round") : null;
            var id = fixture.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                ? idEl.GetInt64().ToString(CultureInfo.InvariantCulture)
                : Guid.NewGuid().ToString("N");

            acc.Add(new FixtureCandidateDto
            {
                ExternalId = $"apifootball-{id}",
                Competition = competition,
                Phase = MapPhase(competition, round),
                HomeTeamName = home!.Trim(),
                AwayTeamName = away!.Trim(),
                StartsAt = startsAt,
                Source = SourceName,
            });
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
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

    /// <summary>
    /// Maps API-Football's <c>league.round</c> string to the internal phase. FA Cup
    /// semis/finals and Championship play-offs get their special multipliers; anything
    /// else is Regular.
    /// </summary>
    private static MatchPhase MapPhase(Competition competition, string? round)
    {
        var r = FootballReference.Normalize(round ?? string.Empty);
        var isFinal = r.Contains("final") && !r.Contains("semi") && !r.Contains("quarter");
        var isSemi = r.Contains("semi");

        return competition switch
        {
            Competition.FACup when isSemi => MatchPhase.FACupSemiFinal,
            Competition.FACup when isFinal => MatchPhase.FACupFinal,
            Competition.Championship when r.Contains("play") && isSemi => MatchPhase.PlayoffSemiFinal,
            Competition.Championship when r.Contains("play") && isFinal => MatchPhase.PlayoffFinal,
            _ => MatchPhase.Regular,
        };
    }
}
