using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;
using Xunit;

namespace Palpitao.Api.Tests.Fixtures;

public class ApiFootballFixtureProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>Returns a canned body per request (keyed by a substring of the URL).</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<string, (HttpStatusCode Status, string Body)> Respond { get; set; } =
            _ => (HttpStatusCode.OK, EmptyResponse);

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            var (status, body) = Respond(url);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private const string EmptyResponse = """{"errors":[],"response":[]}""";

    private static string PremierLeagueResponse => """
    {
      "errors": [],
      "response": [
        {
          "fixture": { "id": 1035038, "date": "2026-08-15T13:30:00+00:00" },
          "league": { "id": 39, "name": "Premier League", "round": "Regular Season - 1" },
          "teams": { "home": { "name": "Arsenal" }, "away": { "name": "Chelsea" } }
        }
      ]
    }
    """;

    private static string FaCupSemiResponse => """
    {
      "errors": [],
      "response": [
        {
          "fixture": { "id": 42, "date": "2026-08-16T15:00:00+00:00" },
          "league": { "id": 45, "name": "FA Cup", "round": "Semi-finals" },
          "teams": { "home": { "name": "Liverpool" }, "away": { "name": "Manchester City" } }
        }
      ]
    }
    """;

    private static ApiFootballFixtureProvider CreateProvider(StubHandler handler, string apiKey = "test-key")
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new FixtureOptions
        {
            Provider = "ApiFootball",
            ApiKey = apiKey,
            ApiFootballBaseUrl = "https://v3.football.api-sports.io",
        });
        return new ApiFootballFixtureProvider(http, options, NullLogger<ApiFootballFixtureProvider>.Instance);
    }

    [Fact]
    public async Task Search_maps_premier_league_fixture()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("league=39")
                ? (HttpStatusCode.OK, PremierLeagueResponse)
                : (HttpStatusCode.OK, EmptyResponse),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        var fixture = Assert.Single(result);
        Assert.Equal(Competition.PremierLeague, fixture.Competition);
        Assert.Equal("Arsenal", fixture.HomeTeamName);
        Assert.Equal("Chelsea", fixture.AwayTeamName);
        Assert.Equal(MatchPhase.Regular, fixture.Phase);
        Assert.Equal("API-Football", fixture.Source);
        Assert.Equal("apifootball-1035038", fixture.ExternalId);
        Assert.Equal(new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc), fixture.StartsAt);
    }

    [Fact]
    public async Task Search_includes_the_api_key_header()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler, apiKey: "abc123");

        await provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        Assert.Single(handler.Requests);
        Assert.Contains("league=39", handler.Requests[0]);
        Assert.Contains("season=2026", handler.Requests[0]);
    }

    [Fact]
    public async Task Search_maps_fa_cup_semifinal_phase()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("league=45")
                ? (HttpStatusCode.OK, FaCupSemiResponse)
                : (HttpStatusCode.OK, EmptyResponse),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.FACup },
            Ct);

        Assert.Equal(MatchPhase.FACupSemiFinal, Assert.Single(result).Phase);
    }

    [Fact]
    public async Task Search_queries_every_requested_competition()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague, Competition.FACup, Competition.Championship, Competition.LeagueOne },
            Ct);

        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains(handler.Requests, u => u.Contains("league=39"));
        Assert.Contains(handler.Requests, u => u.Contains("league=40"));
        Assert.Contains(handler.Requests, u => u.Contains("league=41"));
        Assert.Contains(handler.Requests, u => u.Contains("league=45"));
    }

    [Fact]
    public async Task Search_excludes_fixtures_outside_the_period()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("league=39")
                ? (HttpStatusCode.OK, PremierLeagueResponse) // fixture on 2026-08-15
                : (HttpStatusCode.OK, EmptyResponse),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc), // starts after the fixture
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_throws_on_api_errors_payload()
    {
        var handler = new StubHandler
        {
            Respond = _ => (HttpStatusCode.OK, """{"errors":{"token":"invalid api key"},"response":[]}"""),
        };
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct));

        Assert.Equal("fixtures.fetchFailed", ex.Key);
    }

    [Fact]
    public async Task Search_throws_on_http_error_status()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.TooManyRequests, "{}") };
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct));

        Assert.Equal("fixtures.fetchFailed", ex.Key);
    }

    [Fact]
    public async Task Search_throws_when_key_missing()
    {
        var provider = CreateProvider(new StubHandler(), apiKey: "");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => provider.SearchFixturesAsync(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct));

        Assert.Equal("fixtures.fetchFailed", ex.Key);
    }
}
