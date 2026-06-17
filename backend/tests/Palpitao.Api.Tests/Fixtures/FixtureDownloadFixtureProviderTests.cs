using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Fixtures;
using Xunit;

namespace Palpitao.Api.Tests.Fixtures;

public class FixtureDownloadFixtureProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<string, (HttpStatusCode Status, string Body)> Respond { get; set; } =
            _ => (HttpStatusCode.OK, "[]");

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

    private static string EplFeed => """
    [
      { "MatchNumber": 1, "RoundNumber": 1, "DateUtc": "2025-08-15 19:00:00Z", "HomeTeam": "Liverpool", "AwayTeam": "Bournemouth" },
      { "MatchNumber": 2, "RoundNumber": 1, "DateUtc": "2025-08-16 11:30:00Z", "HomeTeam": "Aston Villa", "AwayTeam": "Newcastle United" },
      { "MatchNumber": 20, "RoundNumber": 2, "DateUtc": "2025-08-23 14:00:00Z", "HomeTeam": "Arsenal", "AwayTeam": "Chelsea" }
    ]
    """;

    private static FixtureDownloadFixtureProvider CreateProvider(StubHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new FixtureOptions
        {
            Provider = "FixtureDownload",
            FixtureDownloadBaseUrl = "https://fixturedownload.com/feed/json",
        });
        return new FixtureDownloadFixtureProvider(
            http, options, NullLogger<FixtureDownloadFixtureProvider>.Instance);
    }

    [Fact]
    public async Task Search_maps_premier_league_fixtures_in_period()
    {
        var handler = new StubHandler
        {
            Respond = url => url.Contains("epl-2025")
                ? (HttpStatusCode.OK, EplFeed)
                : (HttpStatusCode.OK, "[]"),
        };
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        // Only the two fixtures inside 15-17 Aug are returned (the 23rd is excluded).
        Assert.Equal(2, result.Count);
        var first = result.First(f => f.HomeTeamName == "Liverpool");
        Assert.Equal(Competition.PremierLeague, first.Competition);
        Assert.Equal("Bournemouth", first.AwayTeamName);
        Assert.Equal("FixtureDownload", first.Source);
        Assert.Equal(new DateTime(2025, 8, 15, 19, 0, 0, DateTimeKind.Utc), first.StartsAt);
    }

    [Fact]
    public async Task Search_uses_season_start_year_slug()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        await provider.SearchFixturesAsync(
            new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), // Jan 2026 -> 2025 season
            new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.PremierLeague },
            Ct);

        Assert.Contains(handler.Requests, u => u.Contains("epl-2025"));
    }

    [Fact]
    public async Task Search_ignores_unsupported_competitions()
    {
        var handler = new StubHandler();
        var provider = CreateProvider(handler);

        var result = await provider.SearchFixturesAsync(
            new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            new[] { Competition.LeagueOne, Competition.FACup },
            Ct);

        // No feed exists for these, so nothing is requested and nothing returned.
        Assert.Empty(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Search_treats_404_as_empty()
    {
        var handler = new StubHandler { Respond = _ => (HttpStatusCode.NotFound, "<html/>") };
        var provider = CreateProvider(handler);

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
