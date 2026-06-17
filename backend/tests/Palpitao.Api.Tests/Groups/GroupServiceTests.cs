using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Groups;
using Xunit;

namespace Palpitao.Api.Tests.Groups;

public class GroupServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid GroupB = Guid.Parse("99999999-9999-9999-9999-999999999901");

    private static GroupService NewService(AppDbContext db) => new(db);

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
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task SuperAdmin_my_groups_returns_every_group_as_group_admin()
    {
        using var db = CreateContext();
        var service = NewService(db);
        var userId = Guid.NewGuid(); // no memberships at all

        var groups = await service.MyGroupsAsync(userId, isSuperAdmin: true, Ct);

        // Both the seeded default group and Group B, regardless of membership.
        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Equal(GroupRole.GroupAdmin, g.Role));
        Assert.Contains(groups, g => g.GroupId == SeedIds.DefaultGroup);
        Assert.Contains(groups, g => g.GroupId == GroupB);
    }

    [Fact]
    public async Task Regular_user_my_groups_returns_only_approved_memberships()
    {
        using var db = CreateContext();
        var service = NewService(db);

        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "U", Email = "u@x.com", PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = GroupB,
            UserId = userId,
            Role = GroupRole.Participant,
            Status = GroupUserStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var groups = await service.MyGroupsAsync(userId, isSuperAdmin: false, Ct);

        Assert.Single(groups);
        Assert.Equal(GroupB, groups[0].GroupId);
        Assert.Equal(GroupRole.Participant, groups[0].Role);
    }
}
