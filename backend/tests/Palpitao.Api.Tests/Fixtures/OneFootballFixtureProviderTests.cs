using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;
using Xunit;

namespace Palpitao.Api.Tests.Fixtures;

public class OneFootballFixtureProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<string, (HttpStatusCode Status, string Body)> Respond { get; set; } =
            _ => (HttpStatusCode.OK, EmptyContainers);

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

    private const string EmptyContainers = """{"containers":[]}""";

    // Mirrors the real OneFootball web-experience shape: match cards nested deep
    // inside containers, each carrying kickoff + homeTeam.name + awayTeam.name.
    private static string PremierLeaguePayload => """
    {
      "containers": [
        {
          "fullWidth": {
            "component": {
              "matchCardsList": {
                "matchCards": [
                  {
                    "matchId": 111,
                    "link": "/en/match/111",
                    "kickoff": "2025-08-16T14:00:00Z",
                    "period": "PRE_MATCH",
                    "homeTeam": { "name": "Arsenal" },
                    "awayTeam": { "name": "Chelsea" }
                  },
                  {
                    "matchId": 222,
                    "kickoff": "2025-08-23T16:30:00Z",
                    "period": "PRE_MATCH",
                    "homeTeam": { "name": "Liverpool" },
                    "awayTeam": { "name": "Newcastle United" }
                  }
                ]
              }
            }
          }
        }
      ]
    }
    """;

    private static OneFootballFixtureProvider CreateProvider(StubHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new FixtureOptions
        {
            Provider = "OneFootball",
            OneFootballApiBaseUrl = "https://api.onefootball.com/web-experience/en/competition",
        });
        return new OneFootballFixtureProvider(http, options, NullLogger<OneFootballFixtureProvider>.Instance);
    }

    [Fact]
    public async Task Search_extracts_nested_match_cards_in_period()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("premier-league-9")
                ? (HttpStatusCode.OK, PremierLeaguePayload)
                : (HttpStatusCode.OK, EmptyContainers),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        var fixture = Assert.Single(result); // only the 16 Aug card is in the window
        Assert.Equal(Competition.PremierLeague, fixture.Competition);
        Assert.Equal("Arsenal", fixture.HomeTeamName);
        Assert.Equal("Chelsea", fixture.AwayTeamName);
        Assert.Equal("OneFootball", fixture.Source);
        Assert.Equal("onefootball-111", fixture.ExternalId);
        Assert.Equal(new DateTime(2025, 8, 16, 14, 0, 0, DateTimeKind.Utc), fixture.StartsAt);
    }

    [Fact]
    public async Task Search_requests_the_competition_slug()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.LeagueOne },
            Ct);

        Assert.Contains(handler.Requests, u => u.EndsWith("competition/efl-league-one-42/fixtures"));
    }

    [Fact]
    public async Task Search_covers_all_four_competitions()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague, Competition.Championship, Competition.LeagueOne, Competition.FACup },
            Ct);

        Assert.Contains(handler.Requests, u => u.Contains("premier-league-9"));
        Assert.Contains(handler.Requests, u => u.Contains("efl-championship-27"));
        Assert.Contains(handler.Requests, u => u.Contains("efl-league-one-42"));
        Assert.Contains(handler.Requests, u => u.Contains("fa-cup-17"));
    }

    [Fact]
    public async Task Search_returns_empty_off_season_without_error()
    {
        var provider = CreateProvider(new StubHandler()); // every slug -> empty containers

        var result = await provider.SearchFixturesAsync(
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague, Competition.FACup },
            Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Search_tolerates_one_failing_competition()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("premier-league-9")
                ? (HttpStatusCode.OK, PremierLeaguePayload)
                : (HttpStatusCode.InternalServerError, "boom"),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague, Competition.Championship },
            Ct);

        Assert.Equal("Arsenal", Assert.Single(result).HomeTeamName);
    }

    [Fact]
    public async Task Search_throws_when_all_competitions_fail()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.InternalServerError, "boom") };
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => provider.SearchFixturesAsync(
            new DateTime(2025, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague, Competition.Championship },
            Ct));

        Assert.Equal("fixtures.fetchFailed", ex.Key);
    }

    private static string WorldCupPayload => """
    {
      "containers": [{ "fullWidth": { "component": { "matchCardsList": { "matchCards": [
        { "matchId": 901, "kickoff": "2026-06-24T22:00:00Z", "round": "Group C",
          "homeTeam": { "name": "Brazil" }, "awayTeam": { "name": "Scotland" } },
        { "matchId": 902, "kickoff": "2026-06-25T22:00:00Z", "round": "Quarter-final",
          "homeTeam": { "name": "Argentina" }, "awayTeam": { "name": "France" } }
      ] } } } }]
    }
    """;

    [Fact]
    public async Task Search_world_cup_uses_the_world_cup_slug_and_infers_phases()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("fifa-world-cup-12")
                ? (HttpStatusCode.OK, WorldCupPayload)
                : (HttpStatusCode.OK, EmptyContainers),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.FifaWorldCup },
            Ct);

        Assert.Contains(handler.Requests, r => r.Contains("fifa-world-cup-12/fixtures"));
        Assert.All(result, f => Assert.Equal(Competition.FifaWorldCup, f.Competition));
        Assert.Equal(MatchPhase.WorldCupGroupStage, result.Single(f => f.HomeTeamName == "Brazil").Phase);
        Assert.Equal(MatchPhase.WorldCupQuarterFinal, result.Single(f => f.HomeTeamName == "Argentina").Phase);
    }
}
