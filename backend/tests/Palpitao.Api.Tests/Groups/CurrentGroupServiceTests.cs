using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Groups;
using Xunit;

namespace Palpitao.Api.Tests.Groups;

public class CurrentGroupServiceTests
{
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

    /// <summary>Builds the service with an HTTP context carrying the given user id + group header.</summary>
    private static CurrentGroupService Service(AppDbContext db, Guid? userId, string? groupHeader, bool isSuperAdmin = false)
    {
        var ctx = new DefaultHttpContext();
        if (userId is not null)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()!) };
            if (isSuperAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, UserRole.Admin.ToString()));
            }
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }
        if (groupHeader is not null)
        {
            ctx.Request.Headers[CurrentGroupService.GroupHeader] = groupHeader;
        }
        return new CurrentGroupService(db, new HttpContextAccessor { HttpContext = ctx });
    }

    private static Guid SeedUser(AppDbContext db, string email = "u@x.com")
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Name = "U", Email = email, PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return id;
    }

    private static void SeedMembership(AppDbContext db, Guid groupId, Guid userId, GroupRole role, GroupUserStatus status)
    {
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = role,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Approved_member_resolves_group_id_and_role()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);
        SeedMembership(db, SeedIds.DefaultGroup, userId, GroupRole.GroupAdmin, GroupUserStatus.Approved);

        var service = Service(db, userId, SeedIds.DefaultGroup.ToString());

        Assert.Equal(SeedIds.DefaultGroup, await service.GetGroupIdAsync(Ct));
        Assert.Equal(GroupRole.GroupAdmin, await service.GetRoleAsync(Ct));
    }

    [Fact]
    public async Task Missing_header_throws_forbidden()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);

        var service = Service(db, userId, groupHeader: null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetGroupIdAsync(Ct));
    }

    [Fact]
    public async Task Invalid_header_throws_forbidden()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);

        var service = Service(db, userId, groupHeader: "not-a-guid");

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetGroupIdAsync(Ct));
    }

    [Fact]
    public async Task Non_member_throws_forbidden()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);
        // No membership for this group.

        var service = Service(db, userId, SeedIds.DefaultGroup.ToString());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetGroupIdAsync(Ct));
    }

    [Fact]
    public async Task SuperAdmin_gets_group_admin_access_to_any_existing_group_without_membership()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);
        // No membership at all — access comes purely from the platform SuperAdmin role.
        // The default group exists from the seed.

        var service = Service(db, userId, SeedIds.DefaultGroup.ToString(), isSuperAdmin: true);

        Assert.True(service.IsSuperAdmin);
        Assert.Equal(SeedIds.DefaultGroup, await service.GetGroupIdAsync(Ct));
        Assert.Equal(GroupRole.GroupAdmin, await service.GetRoleAsync(Ct));
        await service.RequireGroupAdminAsync(Ct); // does not throw
    }

    [Fact]
    public async Task SuperAdmin_is_denied_for_a_non_existent_group()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);

        var service = Service(db, userId, Guid.NewGuid().ToString(), isSuperAdmin: true);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetGroupIdAsync(Ct));
    }

    [Fact]
    public async Task Non_super_admin_non_member_is_still_denied()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);

        // Same valid, existing group, but no SuperAdmin role and no membership.
        var service = Service(db, userId, SeedIds.DefaultGroup.ToString(), isSuperAdmin: false);

        Assert.False(service.IsSuperAdmin);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetGroupIdAsync(Ct));
    }

    [Fact]
    public async Task Pending_member_is_denied()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);
        SeedMembership(db, SeedIds.DefaultGroup, userId, GroupRole.Participant, GroupUserStatus.PendingApproval);

        var service = Service(db, userId, SeedIds.DefaultGroup.ToString());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RequireApprovedMemberAsync(Ct));
    }

    [Fact]
    public async Task Participant_fails_group_admin_requirement()
    {
        using var db = CreateContext();
        var userId = SeedUser(db);
        SeedMembership(db, SeedIds.DefaultGroup, userId, GroupRole.Participant, GroupUserStatus.Approved);

        var service = Service(db, userId, SeedIds.DefaultGroup.ToString());

        await service.RequireApprovedMemberAsync(Ct); // ok
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RequireGroupAdminAsync(Ct));
    }
}
