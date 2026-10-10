using Microsoft.EntityFrameworkCore;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.Persistence.Seed;
using Palpitao.UnitTests.TestSupport;

namespace Palpitao.UnitTests.Data;

/// <summary>
/// The development admin every test database starts with: most service tests act as this user
/// (<see cref="SeedIds.AdminUser"/>) in the default group.
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
}
