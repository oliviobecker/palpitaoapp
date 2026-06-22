using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Results provider prepared for integration with an external website/API. It is
/// only active when <c>ResultsProvider:Enabled = true</c> and a <c>BaseUrl</c> is set.
///
/// It performs a single GET with a clear user-agent and timeout (no login, no
/// scraping in controllers) and expects a JSON contract:
/// <code>{ "results": [ { "externalMatchId", "url", "homeTeam", "awayTeam", "homeScore", "awayScore", "status" } ] }</code>
/// Anything else raises <c>results.fetchFailed</c> so the admin gets a clear message
/// and the manual flow keeps working. Swap this class to target a different source.
/// </summary>
public class ConfiguredWebsiteResultsProvider : IResultsProvider
{
    private readonly HttpClient _http;
    private readonly ResultsProviderOptions _options;
    private readonly ILogger<ConfiguredWebsiteResultsProvider> _logger;

    public ConfiguredWebsiteResultsProvider(
        HttpClient http,
        IOptions<ResultsProviderOptions> options,
        ILogger<ConfiguredWebsiteResultsProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalpitaoEngland", "1.0"));
        }
    }

    public string Name => "ConfiguredWebsite";

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<IReadOnlyList<ExternalMatchResultDto>> GetResultsForRoundAsync(
        Round round, CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/rounds/{round.Id}/results";

        string payload;
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            payload = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogWarning(ex, "Failed to fetch results from the configured website ({Url}).", url);
            throw new BusinessRuleException("results.fetchFailed");
        }

        return Parse(payload);
    }

    private IReadOnlyList<ExternalMatchResultDto> Parse(string payload)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse the results payload as JSON.");
            throw new BusinessRuleException("results.fetchFailed");
        }

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Results payload did not contain a 'results' array.");
            throw new BusinessRuleException("results.fetchFailed");
        }

        var list = new List<ExternalMatchResultDto>();
        foreach (var item in results.EnumerateArray())
        {
            var home = GetString(item, "homeTeam") ?? GetString(item, "homeTeamName");
            var away = GetString(item, "awayTeam") ?? GetString(item, "awayTeamName");
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            list.Add(new ExternalMatchResultDto
            {
                ExternalMatchId = GetString(item, "externalMatchId") ?? GetString(item, "id"),
                ExternalMatchUrl = GetString(item, "url"),
                HomeTeamName = home.Trim(),
                AwayTeamName = away.Trim(),
                HomeScore = GetInt(item, "homeScore"),
                AwayScore = GetInt(item, "awayScore"),
                Status = ParseStatus(GetString(item, "status")),
            });
        }

        return list;
    }

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

    private static MatchStatus ParseStatus(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "inprogress" or "in_progress" or "live" or "playing" => MatchStatus.InProgress,
            "finished" or "ft" or "fulltime" or "full_time" or "ended" => MatchStatus.Finished,
            "postponed" => MatchStatus.Postponed,
            "cancelled" or "canceled" => MatchStatus.Cancelled,
            _ => MatchStatus.NotStarted,
        };
}
