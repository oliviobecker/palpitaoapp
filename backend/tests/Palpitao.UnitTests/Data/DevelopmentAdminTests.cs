using Microsoft.EntityFrameworkCore;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.Persistence.Seed;
using Palpitao.UnitTests.TestSupport;

namespace Palpitao.UnitTests.Data;

/// <summary>
/// The development admin: every test database starts with it (most service tests act as
/// <see cref="SeedIds.AdminUser"/> in the default group), but it is no longer part of the model's
/// seed, and the runtime seeding never touches a database that already has users.
/// </summary>
public class DevelopmentAdminTests
{
    [Fact]
    public async Task A_fresh_test_database_has_the_development_admin_as_the_default_group_admin()
    {
        using var db = TestDatabase.Create();

        var admin = Assert.Single(await db.Users.ToListAsync());
        Assert.Equal(
            (SeedIds.AdminUser, "admin@palpitao.local", UserRole.Admin, UserStatus.Approved, true),
            (admin.Id, admin.Email, admin.Role, admin.Status, admin.IsActive));

        var membership = Assert.Single(await db.GroupUsers.ToListAsync());
        Assert.Equal(
            (SeedIds.DefaultGroupAdminMembership, SeedIds.DefaultGroup, SeedIds.AdminUser, GroupRole.GroupAdmin, GroupUserStatus.Approved, true),
            (membership.Id, membership.GroupId, membership.UserId, membership.Role, membership.Status, membership.IsActive));
    }

    [Fact]
    public async Task The_model_no_longer_seeds_the_admin_or_its_membership()
    {
        using var db = TestDatabase.Create(withDevelopmentAdmin: false);

        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.GroupUsers.ToListAsync());
        // The default group itself is still seeded: the admin's membership points at it.
        Assert.True(await db.Groups.AnyAsync(g => g.Id == SeedIds.DefaultGroup));
    }

    [Fact]
    public async Task The_admin_is_never_seeded_into_a_database_that_has_users()
    {
        using var db = TestDatabase.Create(withDevelopmentAdmin: false);
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Someone",
            Email = "someone@example.com",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.False(DevelopmentAdmin.SeedIfNoUsers(db));
        Assert.False(await db.Users.AnyAsync(u => u.Id == SeedIds.AdminUser));
        Assert.Empty(await db.GroupUsers.ToListAsync());
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        using var db = TestDatabase.Create();

        Assert.False(DevelopmentAdmin.SeedIfNoUsers(db));
        Assert.Single(await db.Users.ToListAsync());
        Assert.Single(await db.GroupUsers.ToListAsync());
    }

    [Fact]
    public async Task The_published_password_is_flagged_until_it_is_changed()
    {
        using var db = TestDatabase.Create();
        Assert.True(DevelopmentAdmin.HasPublishedPassword(db));

        var admin = await db.Users.SingleAsync(u => u.Id == SeedIds.AdminUser);
        admin.PasswordHash = "$2a$11$another.hash.after.the.password.was.changed.by.its.owner";
        await db.SaveChangesAsync();

        Assert.False(DevelopmentAdmin.HasPublishedPassword(db));
    }
}
