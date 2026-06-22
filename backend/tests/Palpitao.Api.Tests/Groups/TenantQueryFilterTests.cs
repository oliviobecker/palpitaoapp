using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Groups;

/// <summary>
/// Defence-in-depth multi-tenant isolation enforced at the <see cref="AppDbContext"/>
/// level (independent of each service remembering its GroupId filter):
/// the global query filter scopes tenant roots to the request group even when a query
/// omits an explicit GroupId predicate, and SaveChanges stamps the current group on
/// inserts that left GroupId unset (instead of silently defaulting to the default group).
/// </summary>
public class TenantQueryFilterTests
{
    private static readonly Guid GroupA = SeedIds.DefaultGroup;
    private static readonly Guid GroupB = Guid.Parse("88888888-8888-8888-8888-888888888802");
    private static readonly Guid SeasonA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02");
    private static readonly Guid SeasonB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static DbContextOptions<AppDbContext> SeededOptions(SqliteConnection conn)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;

        // Seed across both groups via an unscoped (background-style) context.
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        db.Groups.Add(new Group
        {
            Id = GroupB,
            Name = "Group B",
            Slug = "group-b",
            CreatedByUserId = SeedIds.AdminUser,
            OwnerUserId = SeedIds.AdminUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Seasons.Add(new Season { Id = SeasonA, GroupId = GroupA, Name = "A", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Seasons.Add(new Season { Id = SeasonB, GroupId = GroupB, Name = "B", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return options;
    }

    [Fact]
    public async Task Query_filter_scopes_tenant_roots_to_the_request_group()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = SeededOptions(conn);

        using var scoped = new AppDbContext(options, new FakeRequestGroupContext(GroupA));
        // Deliberately no explicit GroupId predicate: the global filter must still scope it.
        var seasons = await scoped.Seasons.ToListAsync(Ct);

        Assert.Single(seasons);
        Assert.Equal(SeasonA, seasons[0].Id);
    }

    [Fact]
    public async Task No_request_group_sees_all_groups()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = SeededOptions(conn);

        using var unscoped = new AppDbContext(options); // background/seed/test path
        var seasons = await unscoped.Seasons.ToListAsync(Ct);

        Assert.Equal(2, seasons.Count);
    }

    [Fact]
    public async Task Insert_without_group_is_stamped_with_the_request_group()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = SeededOptions(conn);

        var newSeasonId = Guid.NewGuid();
        using (var scoped = new AppDbContext(options, new FakeRequestGroupContext(GroupB)))
        {
            // GroupId left unset (Guid.Empty): must be stamped to Group B, not silently
            // left to the default-group column default.
            scoped.Seasons.Add(new Season { Id = newSeasonId, Name = "stamped", IsActive = false, CreatedAt = DateTime.UtcNow });
            await scoped.SaveChangesAsync(Ct);
        }

        using var unscoped = new AppDbContext(options);
        var saved = await unscoped.Seasons.FirstAsync(s => s.Id == newSeasonId, Ct);
        Assert.Equal(GroupB, saved.GroupId);
    }

    [Fact]
    public async Task Explicit_group_on_insert_is_not_overwritten()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = SeededOptions(conn);

        var newSeasonId = Guid.NewGuid();
        using (var scoped = new AppDbContext(options, new FakeRequestGroupContext(GroupA)))
        {
            // Explicit Group B while acting as Group A: an intentional cross-group write
            // must be left untouched by the stamping.
            scoped.Seasons.Add(new Season { Id = newSeasonId, GroupId = GroupB, Name = "explicit", IsActive = false, CreatedAt = DateTime.UtcNow });
            await scoped.SaveChangesAsync(Ct);
        }

        using var unscoped = new AppDbContext(options);
        var saved = await unscoped.Seasons.FirstAsync(s => s.Id == newSeasonId, Ct);
        Assert.Equal(GroupB, saved.GroupId);
    }

    [Fact]
    public async Task Query_filter_also_scopes_rounds_to_the_request_group()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = SeededOptions(conn);

        using (var seed = new AppDbContext(options))
        {
            seed.Rounds.Add(NewRound(GroupA, SeasonA));
            seed.Rounds.Add(NewRound(GroupB, SeasonB));
            seed.SaveChanges();
        }

        // The same filter applies to every tenant root, not just Season.
        using var scoped = new AppDbContext(options, new FakeRequestGroupContext(GroupB));
        var rounds = await scoped.Rounds.ToListAsync(Ct);

        Assert.Single(rounds);
        Assert.Equal(GroupB, rounds[0].GroupId);
    }

    private static Round NewRound(Guid groupId, Guid seasonId) => new()
    {
        Id = Guid.NewGuid(),
        GroupId = groupId,
        SeasonId = seasonId,
        Number = 1,
        Status = RoundStatus.Draft,
        CreatedByUserId = SeedIds.AdminUser,
        CreatedAt = DateTime.UtcNow,
    };
}
