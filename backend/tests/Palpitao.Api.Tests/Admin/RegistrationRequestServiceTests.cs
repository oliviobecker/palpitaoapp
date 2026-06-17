using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Auth;
using Palpitao.Api.Common;
using Palpitao.Api.Controllers;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Registrations;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Admin;

public class RegistrationRequestServiceTests
{
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly Guid OtherGroup = Guid.Parse("99999999-9999-9999-9999-999999999901");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        // A second group, to assert isolation.
        db.Groups.Add(new Group
        {
            Id = OtherGroup,
            Name = "Outro Grupo",
            Slug = "outro-grupo",
            CreatedByUserId = Admin,
            OwnerUserId = Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db;
    }

    // Service scoped to the seeded default group.
    private static RegistrationRequestService Service(AppDbContext db)
        => new(db, new AuditService(db), new FakeCurrentGroupService());

    /// <summary>Seeds a user + a membership in the given group; returns the GroupUser id.</summary>
    private static Guid SeedMembership(AppDbContext db, Guid groupId, GroupUserStatus status, string email)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Name = $"User {email}",
            Email = email,
            PasswordHash = "x",
            Role = UserRole.Participant,
            Status = UserStatus.Approved,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var membershipId = Guid.NewGuid();
        db.GroupUsers.Add(new GroupUser
        {
            Id = membershipId,
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Participant,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return membershipId;
    }

    [Fact]
    public async Task ListPending_returns_only_pending_requests_of_current_group()
    {
        using var db = CreateContext();
        SeedMembership(db, SeedIds.DefaultGroup, GroupUserStatus.PendingApproval, "pending@x.com");
        SeedMembership(db, SeedIds.DefaultGroup, GroupUserStatus.Approved, "approved@x.com");
        SeedMembership(db, OtherGroup, GroupUserStatus.PendingApproval, "other@x.com");

        var list = await Service(db).ListPendingAsync(Ct);

        Assert.Single(list);
        Assert.Equal("pending@x.com", list[0].Email);
        Assert.Equal(GroupUserStatus.PendingApproval, list[0].Status);
    }

    [Fact]
    public async Task Approve_sets_approved_and_writes_audit()
    {
        using var db = CreateContext();
        var membershipId = SeedMembership(db, SeedIds.DefaultGroup, GroupUserStatus.PendingApproval, "pending@x.com");

        await Service(db).ApproveAsync(membershipId, Admin, Ct);

        var membership = await db.GroupUsers.SingleAsync(gu => gu.Id == membershipId, Ct);
        Assert.Equal(GroupUserStatus.Approved, membership.Status);
        Assert.NotNull(membership.ApprovedAt);
        Assert.Equal(Admin, membership.ApprovedByUserId);
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "RegistrationApproved" && a.GroupId == SeedIds.DefaultGroup);
    }

    [Fact]
    public async Task Reject_sets_rejected_with_reason_and_writes_audit()
    {
        using var db = CreateContext();
        var membershipId = SeedMembership(db, SeedIds.DefaultGroup, GroupUserStatus.PendingApproval, "pending@x.com");

        await Service(db).RejectAsync(membershipId, "Não faz parte do grupo.", Admin, Ct);

        var membership = await db.GroupUsers.SingleAsync(gu => gu.Id == membershipId, Ct);
        Assert.Equal(GroupUserStatus.Rejected, membership.Status);
        Assert.NotNull(membership.RejectedAt);
        Assert.Equal(Admin, membership.RejectedByUserId);
        Assert.Equal("Não faz parte do grupo.", membership.RejectionReason);
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "RegistrationRejected");
    }

    [Fact]
    public async Task Cannot_approve_request_of_another_group()
    {
        using var db = CreateContext();
        var otherMembership = SeedMembership(db, OtherGroup, GroupUserStatus.PendingApproval, "other@x.com");

        // The service is scoped to the default group, so the other group's request is invisible.
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).ApproveAsync(otherMembership, Admin, Ct));
    }

    [Fact]
    public async Task Approve_throws_when_not_pending()
    {
        using var db = CreateContext();
        var membershipId = SeedMembership(db, SeedIds.DefaultGroup, GroupUserStatus.Approved, "approved@x.com");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).ApproveAsync(membershipId, Admin, Ct));
        Assert.Equal("registration.notPending", ex.Key);
    }

    [Fact]
    public async Task Get_throws_for_unknown_request()
    {
        using var db = CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public void Controller_requires_group_admin()
    {
        // Approving/rejecting is gated by [RequireGroupAdmin] on the controller, so only
        // approved group admins of the current group can reach the endpoints.
        var requireGroupAdmin = Attribute.GetCustomAttribute(
            typeof(AdminRegistrationRequestsController), typeof(RequireGroupAdminAttribute));

        Assert.NotNull(requireGroupAdmin);
    }
}
