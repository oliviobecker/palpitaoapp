using System.Net;
using Palpitao.Domain.Common;
using Palpitao.Domain.Entities;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.IntegrationTests;

/// <summary>
/// The one anonymous read path: the season's public key is the credential, no session or group
/// header is needed, and a stray group header from a signed-in browser must not hide the season.
/// </summary>
public class PublicStandingsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private async Task<string> AddPublicSeasonAsync(bool enabled = true)
    {
        var key = PublicKeyGenerator.Generate();
        await api.WithDbAsync(async db =>
        {
            db.Seasons.Add(new Season
            {
                Id = Guid.NewGuid(),
                GroupId = SeedIds.DefaultGroup,
                Name = "Public season",
                StartDate = new DateOnly(2026, 8, 1),
                EndDate = new DateOnly(2027, 5, 31),
                PublicKey = key,
                PublicStandingsEnabled = enabled,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        return key;
    }

    [Fact]
    public async Task Anyone_with_the_key_reads_the_standings()
    {
        var key = await AddPublicSeasonAsync();

        var response = await api.CreateClient().GetAsync($"/public/seasons/{key}/standings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_stray_group_header_does_not_hide_the_season()
    {
        var key = await AddPublicSeasonAsync();
        var otherGroup = await api.AddGroupAsync("Signed-in elsewhere");
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Group-Id", otherGroup.ToString());

        var response = await client.GetAsync($"/public/seasons/{key}/standings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_season_whose_link_is_off_is_not_found()
    {
        var key = await AddPublicSeasonAsync(enabled: false);

        var response = await api.CreateClient("en-US").GetAsync($"/public/seasons/{key}/standings");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "Season not found.");
    }
}
