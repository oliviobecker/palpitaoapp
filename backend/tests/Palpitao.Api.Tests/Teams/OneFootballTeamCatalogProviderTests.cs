using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;
using Palpitao.Api.Services.Teams;
using Xunit;

namespace Palpitao.Api.Tests.Teams;

public class OneFootballTeamCatalogProviderTests
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

    // Upcoming games carry a kickoff.
    private static string FixturesPayload => """
    {
      "containers": [
        {
          "fullWidth": {
            "component": {
              "matchCardsList": {
                "matchCards": [
                  {
                    "matchId": 111,
                    "kickoff": "2026-08-16T14:00:00Z",
                    "homeTeam": { "name": "Arsenal" },
                    "awayTeam": { "name": "Chelsea" }
                  }
                ]
              }
            }
          }
        }
      ]
    }
    """;

    // Played games have no kickoff — the roster still has to pick them up, which is
    // the whole reason the provider reads both tabs.
    private static string ResultsPayload => """
    {
      "containers": [
        {
          "fullWidth": {
            "component": {
              "matchCardsList": {
                "matchCards": [
                  {
                    "matchId": 222,
                    "scoreLine": "2:1",
                    "homeTeam": { "name": "Liverpool" },
                    "awayTeam": { "name": "Arsenal" }
                  }
                ]
              }
            }
          }
        }
      ]
    }
    """;

    private static OneFootballTeamCatalogProvider CreateProvider(StubHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new FixtureOptions
        {
            OneFootballApiBaseUrl = "https://api.onefootball.com/web-experience/en/competition",
        });
        return new OneFootballTeamCatalogProvider(
            http, options, NullLogger<OneFootballTeamCatalogProvider>.Instance);
    }

    [Fact]
    public async Task Unions_the_names_of_both_tabs_without_duplicates()
    {
        var handler = new StubHandler
        {
            Respond = url => url.EndsWith("/results")
                ? (HttpStatusCode.OK, ResultsPayload)
                : (HttpStatusCode.OK, FixturesPayload),
        };
        var provider = CreateProvider(handler);

        var names = await provider.GetTeamNamesAsync(Competition.PremierLeague, Ct);

        // Arsenal plays on both tabs but must be listed once.
        Assert.Equal(new[] { "Arsenal", "Chelsea", "Liverpool" }, names.OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task Requests_both_tabs_of_the_competition_slug()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.GetTeamNamesAsync(Competition.Championship, Ct);

        Assert.Contains(handler.Requests, u => u.EndsWith("competition/efl-championship-27/fixtures"));
        Assert.Contains(handler.Requests, u => u.EndsWith("competition/efl-championship-27/results"));
    }

    [Fact]
    public async Task Returns_nothing_when_both_tabs_are_missing()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.NotFound, "") };
        var provider = CreateProvider(handler);

        var names = await provider.GetTeamNamesAsync(Competition.LeagueOne, Ct);

        // Off-season: no data is not the same as "no teams", and the caller must be
        // able to tell them apart.
        Assert.Empty(names);
    }

    [Fact]
    public async Task Keeps_the_names_of_the_tab_that_answered()
    {
        var handler = new StubHandler
        {
            Respond = url => url.EndsWith("/fixtures")
                ? (HttpStatusCode.NotFound, "")
                : (HttpStatusCode.OK, ResultsPayload),
        };
        var provider = CreateProvider(handler);

        var names = await provider.GetTeamNamesAsync(Competition.PremierLeague, Ct);

        Assert.Equal(new[] { "Arsenal", "Liverpool" }, names.OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task Fails_the_whole_sync_when_a_tab_errors()
    {
        var handler = new StubHandler
        {
            Respond = url => url.EndsWith("/results")
                ? (HttpStatusCode.InternalServerError, "boom")
                : (HttpStatusCode.OK, FixturesPayload),
        };
        var provider = CreateProvider(handler);

        // A half-loaded roster would report every missing club as "not in the source".
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => provider.GetTeamNamesAsync(Competition.PremierLeague, Ct));
        Assert.Equal("teams.syncFailed", ex.Key);
    }

    [Fact]
    public async Task Fails_when_the_payload_is_not_json()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.OK, "<html>nope</html>") };
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => provider.GetTeamNamesAsync(Competition.PremierLeague, Ct));
        Assert.Equal("teams.syncFailed", ex.Key);
    }

    [Theory]
    [InlineData(Competition.FACup)]
    [InlineData(Competition.FifaWorldCup)]
    public async Task Skips_competitions_that_are_not_divisions(Competition competition)
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        var names = await provider.GetTeamNamesAsync(competition, Ct);

        Assert.Empty(names);
        Assert.Empty(handler.Requests);
    }
}
