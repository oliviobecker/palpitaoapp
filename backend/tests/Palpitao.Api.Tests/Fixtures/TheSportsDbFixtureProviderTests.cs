using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;
using Xunit;

namespace Palpitao.Api.Tests.Fixtures;

public class TheSportsDbFixtureProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

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

    private const string EmptyResponse = """{"events":null}""";

    private static string PremierLeagueResponse => """
    {
      "events": [
        {
          "idEvent": "2267073",
          "strHomeTeam": "Liverpool",
          "strAwayTeam": "Bournemouth",
          "strSeason": "2025-2026",
          "intRound": "1",
          "strTimestamp": "2025-08-15T19:00:00",
          "dateEvent": "2025-08-15",
          "strTime": "19:00:00"
        },
        {
          "idEvent": "2267099",
          "strHomeTeam": "Arsenal",
          "strAwayTeam": "Chelsea",
          "strSeason": "2025-2026",
          "intRound": "2",
          "strTimestamp": "2025-08-23T11:30:00",
          "dateEvent": "2025-08-23",
          "strTime": "11:30:00"
        }
      ]
    }
    """;

    private static TheSportsDbFixtureProvider CreateProvider(StubHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new FixtureOptions
        {
            Provider = "TheSportsDb",
            TheSportsDbKey = "3",
            TheSportsDbBaseUrl = "https://www.thesportsdb.com/api/v1/json",
        });
        return new TheSportsDbFixtureProvider(http, options, NullLogger<TheSportsDbFixtureProvider>.Instance);
    }

    [Fact]
    public async Task Search_maps_current_season_fixture_in_period()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("id=4328")
                ? (HttpStatusCode.OK, PremierLeagueResponse)
                : (HttpStatusCode.OK, EmptyResponse),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        var fixture = Assert.Single(result);
        Assert.Equal(Competition.PremierLeague, fixture.Competition);
        Assert.Equal("Liverpool", fixture.HomeTeamName);
        Assert.Equal("Bournemouth", fixture.AwayTeamName);
        Assert.Equal("TheSportsDB", fixture.Source);
        Assert.Equal("thesportsdb-2267073", fixture.ExternalId);
        Assert.Equal(new DateTime(2025, 8, 15, 19, 0, 0, DateTimeKind.Utc), fixture.StartsAt);
    }

    [Fact]
    public async Task Search_excludes_fixtures_outside_the_period()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("id=4328")
                ? (HttpStatusCode.OK, PremierLeagueResponse)
                : (HttpStatusCode.OK, EmptyResponse),
        };
        var provider = CreateProvider(handler);

        // Window covers only 2025-08-15; the 2025-08-23 fixture must be excluded.
        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        Assert.Equal("Liverpool", Assert.Single(result).HomeTeamName);
    }

    [Fact]
    public async Task Search_uses_the_configured_key_and_league_id()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        Assert.Contains(handler.Requests, u => u.Contains("/3/eventsseason.php"));
        Assert.Contains(handler.Requests, u => u.Contains("id=4328"));
        Assert.Contains(handler.Requests, u => u.Contains("s=2025-2026"));
    }

    [Fact]
    public async Task Search_queries_every_tracked_competition()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague, Competition.Championship, Competition.LeagueOne, Competition.FACup },
            Ct);

        Assert.Contains(handler.Requests, u => u.Contains("id=4328"));
        Assert.Contains(handler.Requests, u => u.Contains("id=4329"));
        Assert.Contains(handler.Requests, u => u.Contains("id=4396"));
        Assert.Contains(handler.Requests, u => u.Contains("id=4482"));
    }

    [Fact]
    public async Task Search_returns_empty_when_no_events()
    {
        var provider = CreateProvider(new StubHandler()); // every league -> {"events":null}

        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_throws_on_http_error_status()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.InternalServerError, "{}") };
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct));

        Assert.Equal("fixtures.fetchFailed", ex.Key);
    }
}
