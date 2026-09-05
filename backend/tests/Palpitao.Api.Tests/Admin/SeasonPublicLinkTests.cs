using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Seasons;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Seasons;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Admin;

/// <summary>
/// The admin side of the public standings link: the key exists from creation, publishing is
/// a separate deliberate act, and regenerating cuts the shared link.
/// </summary>
public class SeasonPublicLinkTests
{
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static SeasonService Build(AppDbContext db)
        => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static SeasonRequest Request(string name = "Season") => new()
    {
        Name = name,
        StartDate = new DateOnly(2025, 8, 1),
        EndDate = new DateOnly(2026, 5, 31),
    };

    [Fact]
    public async Task A_new_season_has_a_key_but_is_not_published()
    {
        using var db = CreateContext();

        var created = await Build(db).CreateAsync(Request(), Admin, Ct);

        Assert.Equal(12, created.PublicKey.Length);
        Assert.False(created.PublicStandingsEnabled);
    }

    [Fact]
    public async Task Publishing_is_a_deliberate_act_and_is_audited()
    {
        using var db = CreateContext();
        var service = Build(db);
        var created = await service.CreateAsync(Request(), Admin, Ct);

        var request = Request();
        request.PublicStandingsEnabled = true;
        var updated = await service.UpdateAsync(created.Id, request, Admin, Ct);

        Assert.True(updated.PublicStandingsEnabled);
        Assert.Contains(db.AuditLogs, a => a.Action == "SeasonUpdated");
    }

    [Fact]
    public async Task Regenerating_replaces_the_key_and_is_audited()
    {
        using var db = CreateContext();
        var service = Build(db);
        var created = await service.CreateAsync(Request(), Admin, Ct);

        var regenerated = await service.RegeneratePublicKeyAsync(created.Id, Admin, Ct);

        Assert.NotEqual(created.PublicKey, regenerated.PublicKey);
        Assert.Equal(12, regenerated.PublicKey.Length);
        // Cutting a link that people already hold leaves no other visible trace.
        Assert.Contains(db.AuditLogs, a => a.Action == "SeasonPublicKeyRegenerated");
    }

    [Fact]
    public async Task Regenerating_does_not_change_whether_the_link_is_published()
    {
        using var db = CreateContext();
        var service = Build(db);
        var request = Request();
        request.PublicStandingsEnabled = true;
        var created = await service.CreateAsync(request, Admin, Ct);

        var regenerated = await service.RegeneratePublicKeyAsync(created.Id, Admin, Ct);

        Assert.True(regenerated.PublicStandingsEnabled);
    }

    [Fact]
    public async Task The_key_survives_an_ordinary_update()
    {
        using var db = CreateContext();
        var service = Build(db);
        var created = await service.CreateAsync(Request(), Admin, Ct);

        var updated = await service.UpdateAsync(created.Id, Request("Renamed"), Admin, Ct);

        // Renaming a season must not silently break a link that is already circulating.
        Assert.Equal(created.PublicKey, updated.PublicKey);
    }

    [Fact]
    public async Task Listing_exposes_the_key_so_the_admin_can_share_it()
    {
        using var db = CreateContext();
        var service = Build(db);
        var created = await service.CreateAsync(Request(), Admin, Ct);

        var listed = Assert.Single(await service.ListAsync(Ct));

        Assert.Equal(created.PublicKey, listed.PublicKey);
    }
}
