using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Seasons;
using Palpitao.Api.Services.Standings;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Groups;

/// <summary>
/// End-to-end-ish isolation checks: two groups never see each other's rounds,
/// seasons or standings, and cross-group access by id is rejected.
/// </summary>
public class GroupIsolationTests
{
    private static readonly Guid GroupA = SeedIds.DefaultGroup;
    private static readonly Guid GroupB = Guid.Parse("88888888-8888-8888-8888-888888888801");
    private static readonly Guid SeasonA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    private static readonly Guid SeasonB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
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
        return db;
    }

    private static RoundService Rounds(AppDbContext db, Guid groupId)
        => new(db, new AuditService(db), new FakeCurrentGroupService(groupId));

    private static StandingsService Standings(AppDbContext db, Guid groupId)
        => new(db, new FakeCurrentGroupService(groupId));

    private static void SeedRound(AppDbContext db, Guid groupId, Guid seasonId, int number)
    {
        db.Rounds.Add(new Round
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonId = seasonId,
            Number = number,
            Status = RoundStatus.Draft,
            CreatedByUserId = SeedIds.AdminUser,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Rounds_are_listed_only_for_the_current_group()
    {
        using var db = CreateContext();
        SeedRound(db, GroupA, SeasonA, 1);
        SeedRound(db, GroupA, SeasonA, 2);
        SeedRound(db, GroupB, SeasonB, 1);

        var aRounds = await Rounds(db, GroupA).GetAllAsync(Ct);
        var bRounds = await Rounds(db, GroupB).GetAllAsync(Ct);

        Assert.Equal(2, aRounds.Count);
        Assert.Single(bRounds);
    }

    [Fact]
    public async Task Cannot_read_a_round_from_another_group_by_id()
    {
        using var db = CreateContext();
        SeedRound(db, GroupB, SeasonB, 1);
        var bRoundId = await db.Rounds.Where(r => r.GroupId == GroupB).Select(r => r.Id).FirstAsync(Ct);

        // Group A admin asking for Group B's round -> not found.
        await Assert.ThrowsAsync<NotFoundException>(() => Rounds(db, GroupA).GetByIdAsync(bRoundId, Ct));
        // Group B can read it.
        var dto = await Rounds(db, GroupB).GetByIdAsync(bRoundId, Ct);
        Assert.Equal(bRoundId, dto.Id);
    }

    [Fact]
    public async Task Standings_are_scoped_to_the_current_group()
    {
        using var db = CreateContext();
        var userA = AddMember(db, GroupA);
        var userB = AddMember(db, GroupB);
        AddStanding(db, GroupA, SeasonA, userA, 30);
        AddStanding(db, GroupB, SeasonB, userB, 50);

        var aStandings = await Standings(db, GroupA).GetStandingsAsync(SeasonA, Ct);
        Assert.Single(aStandings);
        Assert.Equal(userA, aStandings[0].UserId);

        // Group A cannot read Group B's season standings.
        await Assert.ThrowsAsync<NotFoundException>(() => Standings(db, GroupA).GetStandingsAsync(SeasonB, Ct));
    }

    [Fact]
    public async Task Per_group_active_and_elimination_flags_do_not_leak_across_groups()
    {
        using var db = CreateContext();
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "U", Email = "u@x.com", PasswordHash = "x", IsActive = true, CreatedAt = now });
        // Same global user: eliminated in Group A, but active & not eliminated in Group B.
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(), GroupId = GroupA, UserId = userId,
            Role = GroupRole.Participant, Status = GroupUserStatus.Approved,
            IsActive = true, IsEliminated = true, CreatedAt = now, UpdatedAt = now,
        });
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(), GroupId = GroupB, UserId = userId,
            Role = GroupRole.Participant, Status = GroupUserStatus.Approved,
            IsActive = true, IsEliminated = false, CreatedAt = now, UpdatedAt = now,
        });
        db.SaveChanges();

        var activeA = await GroupQueries.ActiveParticipants(db, GroupA).Select(u => u.Id).ToListAsync(Ct);
        var activeB = await GroupQueries.ActiveParticipants(db, GroupB).Select(u => u.Id).ToListAsync(Ct);

        Assert.DoesNotContain(userId, activeA); // eliminated in A
        Assert.Contains(userId, activeB);       // still active in B
    }

    private static Guid AddMember(AppDbContext db, Guid groupId)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Name = $"U{id.ToString()[..4]}", Email = $"{id}@x.com", PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = id,
            Role = GroupRole.Participant,
            Status = GroupUserStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    private static void AddStanding(AppDbContext db, Guid groupId, Guid seasonId, Guid userId, int points)
    {
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonId = seasonId,
            UserId = userId,
            TotalPoints = points,
            Position = 1,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }
}
