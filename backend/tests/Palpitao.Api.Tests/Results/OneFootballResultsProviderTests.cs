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

    private static Round PremierLeagueRound() =>
        new()
        {
            Id = Guid.NewGuid(),
            Matches =
            {
                new RoundMatch
                {
                    Id = Guid.NewGuid(),
                    Competition = Competition.PremierLeague,
                    Phase = MatchPhase.Regular,
                },
            },
        };

    private static string Payload(string card) => $$"""
    { "containers": [{ "c": { "matchCards": [ {{card}} ] } }] }
    """;

    // The real web-experience cards: string matchId, string scores, and the state in `period`
    // with the clock in `timePeriod`.
    private const string FinishedCard = """
    { "matchId": "901", "link": "/en/match/901", "period": "FULL_TIME", "timePeriod": "Full time",
      "homeTeam": { "name": "Brazil", "score": "3" },
      "awayTeam": { "name": "Scotland", "score": "0" } }
    """;

    private const string LiveCard = """
    { "matchId": "2693565", "link": "/en/match/2693565", "period": "SECOND_HALF", "timePeriod": "71'",
      "homeTeam": { "name": "Arsenal", "score": "3" },
      "awayTeam": { "name": "Coventry City", "score": "0" } }
    """;

    private const string PreMatchCard = """
    { "matchId": "2693566", "link": "/en/match/2693566", "period": "PRE_MATCH",
      "homeTeam": { "name": "Hull City" },
      "awayTeam": { "name": "Manchester United" } }
    """;

    private const string AbandonedCard = """
    { "matchId": "2693567", "link": "/en/match/2693567", "period": "ABANDONED", "timePeriod": "Abandoned",
      "homeTeam": { "name": "Everton", "score": "1" },
      "awayTeam": { "name": "Crystal Palace", "score": "1" } }
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
                ? (HttpStatusCode.OK, Payload(FinishedCard))
                : (HttpStatusCode.OK, "{}"),
        };

        var results = await CreateProvider(handler).GetResultsForRoundAsync(WorldCupRound(), Ct);

        var match = Assert.Single(results, r => r.ExternalMatchId == "onefootball-901");
        Assert.Equal(3, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Contains(handler.Requests, r => r.Contains("fifa-world-cup-12/fixtures"));
    }

    /// <summary>
    /// The regression test for the reported bug: a match being played was read as "not started",
    /// so the admin refresh reported "Em andamento: 0" with the live scoreline already in hand.
    /// </summary>
    [Fact]
    public async Task Reads_a_live_second_half_card_as_in_progress()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.OK, Payload(LiveCard)) };

        var results = await CreateProvider(handler).GetResultsForRoundAsync(PremierLeagueRound(), Ct);

        var match = Assert.Single(results);
        Assert.Equal(MatchStatus.InProgress, match.Status);
        Assert.Equal("Arsenal", match.HomeTeamName);
        Assert.Equal(3, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
        Assert.Equal("onefootball-2693565", match.ExternalMatchId);
        // Stamped from the tab it was read off, so a cup tie between the same clubs cannot be
        // mistaken for this one.
        Assert.Equal(Competition.PremierLeague, match.Competition);
    }

    [Fact]
    public async Task Reads_a_pre_match_card_as_not_started()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.OK, Payload(PreMatchCard)) };

        var results = await CreateProvider(handler).GetResultsForRoundAsync(PremierLeagueRound(), Ct);

        var match = Assert.Single(results);
        Assert.Equal(MatchStatus.NotStarted, match.Status);
        Assert.Null(match.HomeScore);
        Assert.Null(match.AwayScore);
    }

    [Fact]
    public async Task Reads_an_abandoned_card_as_cancelled()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.OK, Payload(AbandonedCard)) };

        var results = await CreateProvider(handler).GetResultsForRoundAsync(PremierLeagueRound(), Ct);

        Assert.Equal(MatchStatus.Cancelled, Assert.Single(results).Status);
    }

    /// <summary>
    /// Neither tab is authoritative — "results" lists not-yet-played fixtures too — so the card
    /// that knows the most about the match has to win whichever tab it came from.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_played_card_wins_over_a_not_started_duplicate(bool playedOnResultsTab)
    {
        const string sameMatchNotStarted = """
        { "matchId": "777", "link": "/en/match/777", "period": "PRE_MATCH",
          "homeTeam": { "name": "Arsenal" }, "awayTeam": { "name": "Chelsea" } }
        """;
        const string sameMatchPlayed = """
        { "matchId": "777", "link": "/en/match/777", "period": "FULL_TIME", "timePeriod": "Full time",
          "homeTeam": { "name": "Arsenal", "score": "2" },
          "awayTeam": { "name": "Chelsea", "score": "1" } }
        """;

        var handler = new StubHandler
        {
            Respond = url => (HttpStatusCode.OK, Payload(
                url.Contains("/results") == playedOnResultsTab ? sameMatchPlayed : sameMatchNotStarted)),
        };

        var results = await CreateProvider(handler).GetResultsForRoundAsync(PremierLeagueRound(), Ct);

        var match = Assert.Single(results);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(2, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }
}
