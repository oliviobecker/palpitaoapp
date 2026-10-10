using System.Net;
using System.Net.Http.Json;
using Palpitao.Application.Seasons;
using Palpitao.Domain.Entities;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.IntegrationTests;

/// <summary>
/// The group chokepoint over real HTTP: the X-Group-Id header is revalidated against an approved,
/// active membership on every call, and another group's data never comes back.
/// </summary>
public class TenantIsolationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string Password = "Partic1pant!";

    [Fact]
    public async Task A_member_reads_their_own_group()
    {
        var email = $"member-{Guid.NewGuid():N}@example.com";
        await api.AddParticipantAsync(email, Password, SeedIds.DefaultGroup);
        var client = await api.SignedInClientAsync(email, Password, SeedIds.DefaultGroup);

        var response = await client.GetAsync("/seasons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Another_group_s_header_is_refused()
    {
        var email = $"member-{Guid.NewGuid():N}@example.com";
        await api.AddParticipantAsync(email, Password, SeedIds.DefaultGroup);
        var otherGroup = await api.AddGroupAsync("Other group");
        var client = await api.SignedInClientAsync(email, Password, otherGroup);

        var response = await client.GetAsync("/seasons");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.Forbidden, "You do not have access to this group.");
    }

    [Fact]
    public async Task A_missing_group_header_is_refused()
    {
        var email = $"member-{Guid.NewGuid():N}@example.com";
        await api.AddParticipantAsync(email, Password, SeedIds.DefaultGroup);
        var client = await api.SignedInClientAsync(email, Password);

        var response = await client.GetAsync("/seasons");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.Forbidden, "Select a group to continue.");
    }

    [Fact]
    public async Task A_deactivated_membership_is_refused()
    {
        var email = $"member-{Guid.NewGuid():N}@example.com";
        await api.AddParticipantAsync(email, Password, SeedIds.DefaultGroup, activeInGroup: false);
        var client = await api.SignedInClientAsync(email, Password, SeedIds.DefaultGroup);

        var response = await client.GetAsync("/seasons");

        await ProblemAssert.IsProblemAsync(
            response, HttpStatusCode.Forbidden,
            "Your access to this group has been deactivated. Contact the administrator.");
    }

    [Fact]
    public async Task Another_group_s_season_never_appears()
    {
        var otherGroup = await api.AddGroupAsync("Hidden group");
        var hidden = new Season
        {
            Id = Guid.NewGuid(),
            GroupId = otherGroup,
            Name = "Somebody else's season",
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2027, 5, 31),
            CreatedAt = DateTime.UtcNow,
        };
        await api.WithDbAsync(async db => { db.Seasons.Add(hidden); await db.SaveChangesAsync(); });
        var email = $"member-{Guid.NewGuid():N}@example.com";
        await api.AddParticipantAsync(email, Password, SeedIds.DefaultGroup);
        var client = await api.SignedInClientAsync(email, Password, SeedIds.DefaultGroup);

        var seasons = await client.GetFromJsonAsync<List<SeasonDto>>("/seasons", ApiFactory.Json);

        Assert.DoesNotContain(seasons!, s => s.Id == hidden.Id);
    }
}
