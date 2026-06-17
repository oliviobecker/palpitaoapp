using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;
using Palpitao.Api.Services.Results;
using Xunit;

namespace Palpitao.Api.Tests.Results;

public class OneFootballResultsProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<string, (HttpStatusCode, string)> Respond { get; set; } = _ => (HttpStatusCode.OK, "{}");
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.ToString());
            var (status, body) = Respond(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static OneFootballResultsProvider CreateProvider(StubHandler handler, bool enabled = true)
        => new(
            new HttpClient(handler),
            Options.Create(new ResultsProviderOptions { Provider = "OneFootball", Enabled = enabled }),
            Options.Create(new FixtureOptions
            {
                OneFootballApiBaseUrl = "https://api.onefootball.com/web-experience/en/competition",
            }),
            NullLogger<OneFootballResultsProvider>.Instance);

    private static Round WorldCupRound() =>
        new()
        {
            Id = Guid.NewGuid(),
            Matches =
            {
                new RoundMatch
                {
                    Id = Guid.NewGuid(),
                    Competition = Competition.FifaWorldCup,
                    Phase = MatchPhase.WorldCupGroupStage,
                    ExternalMatchId = "onefootball-901",
                },
            },
        };

    private const string FinishedPayload = """
    {
      "containers": [{ "c": { "matchCards": [
        { "matchId": 901, "link": "/en/match/901", "period": "FT",
          "homeTeam": { "name": "Brazil", "score": 3 },
          "awayTeam": { "name": "Scotland", "score": 0 } }
      ] } }]
    }
    """;

    [Fact]
    public void Is_disabled_when_not_enabled()
    {
        Assert.False(CreateProvider(new StubHandler(), enabled: false).IsEnabled);
        Assert.True(CreateProvider(new StubHandler(), enabled: true).IsEnabled);
    }

    [Fact]
    public async Task Reads_world_cup_scores_and_status_keyed_by_external_id()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("fifa-world-cup-12")
                ? (HttpStatusCode.OK, FinishedPayload)
                : (HttpStatusCode.OK, "{}"),
        };

        var results = await CreateProvider(handler).GetResultsForRoundAsync(WorldCupRound(), Ct);

        var match = Assert.Single(results, r => r.ExternalMatchId == "onefootball-901");
        Assert.Equal(3, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Contains(handler.Requests, r => r.Contains("fifa-world-cup-12/fixtures"));
    }
}
