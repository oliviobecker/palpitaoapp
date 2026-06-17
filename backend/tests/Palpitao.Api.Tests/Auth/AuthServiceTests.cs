using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Auth;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Auth;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Auth;
using Xunit;

namespace Palpitao.Api.Tests.Auth;

public class AuthServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class FakeJwt : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user)
            => ($"token-{user.Id}", DateTime.UtcNow.AddHours(1));
    }

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AuthService Service(AppDbContext db) => new(db, new FakeJwt(), new AuditService(db));

    private static RegisterRequest Reg(string email = "novo@x.com", string password = "Senha123", string? confirm = null, Guid? groupId = null) => new()
    {
        Name = "Novo Usuário",
        Email = email,
        Password = password,
        ConfirmPassword = confirm ?? password,
        // Default group is seeded by EnsureCreated.
        GroupId = groupId ?? SeedIds.DefaultGroup,
    };

    private static Guid SeedUser(AppDbContext db, UserStatus status, bool active, string email = "user@x.com", string password = "Senha123")
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = "User",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Participant,
            Status = status,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    // --- Register -----------------------------------------------------------

    [Fact]
    public async Task Register_creates_active_account_and_pending_group_membership_with_hashed_password()
    {
        using var db = CreateContext();

        await Service(db).RegisterAsync(Reg(), Ct);

        // The global account is a valid identity; group membership gates access.
        var user = await db.Users.SingleAsync(u => u.Email == "novo@x.com", Ct);
        Assert.Equal(UserRole.Participant, user.Role);
        Assert.Equal(UserStatus.Approved, user.Status);
        Assert.True(user.IsActive);
        Assert.NotEqual("Senha123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Senha123", user.PasswordHash));

        var membership = await db.GroupUsers.SingleAsync(gu => gu.UserId == user.Id, Ct);
        Assert.Equal(SeedIds.DefaultGroup, membership.GroupId);
        Assert.Equal(GroupRole.Participant, membership.Role);
        Assert.Equal(GroupUserStatus.PendingApproval, membership.Status);

        var audit = Assert.Single(await db.AuditLogs.Where(a => a.Action == "RegistrationSubmitted").ToListAsync(Ct));
        Assert.Equal(SeedIds.DefaultGroup, audit.GroupId);
    }

    [Fact]
    public async Task Register_requires_a_group()
    {
        using var db = CreateContext();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db).RegisterAsync(Reg(groupId: Guid.Empty), Ct));
        Assert.Equal("group.required", ex.Key);
    }

    [Fact]
    public async Task Register_rejects_unknown_group()
    {
        using var db = CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => Service(db).RegisterAsync(Reg(groupId: Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Register_with_existing_email_reuses_account_and_adds_membership()
    {
        using var db = CreateContext();
        SeedUser(db, UserStatus.Approved, active: true, email: "dup@x.com");

        await Service(db).RegisterAsync(Reg(email: "dup@x.com"), Ct);

        // No second account created; a pending membership is added for the group.
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "dup@x.com", Ct));
        var user = await db.Users.SingleAsync(u => u.Email == "dup@x.com", Ct);
        var membership = await db.GroupUsers.SingleAsync(gu => gu.UserId == user.Id, Ct);
        Assert.Equal(GroupUserStatus.PendingApproval, membership.Status);
    }

    [Fact]
    public async Task Register_rejects_duplicate_membership_in_same_group()
    {
        using var db = CreateContext();
        await Service(db).RegisterAsync(Reg(email: "twice@x.com"), Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db).RegisterAsync(Reg(email: "twice@x.com"), Ct));
        Assert.Equal("group.alreadyMember", ex.Key);
    }

    [Fact]
    public async Task Register_rejects_password_confirmation_mismatch()
    {
        using var db = CreateContext();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db).RegisterAsync(Reg(password: "Senha123", confirm: "Outra123"), Ct));
        Assert.Equal("auth.passwordMismatch", ex.Key);
    }

    [Theory]
    [InlineData("Curta1")]      // too short
    [InlineData("semnumeros")]  // no digit
    [InlineData("12345678")]    // no letter
    public async Task Register_rejects_weak_passwords(string password)
    {
        using var db = CreateContext();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db).RegisterAsync(Reg(password: password), Ct));
        Assert.Equal("auth.weakPassword", ex.Key);
    }

    // --- Create group -------------------------------------------------------

    private static Palpitao.Api.DTOs.Groups.CreateGroupRequest NewGroup(
        string groupName = "Grupo dos Amigos", string email = "owner@x.com", string password = "Senha123") => new()
        {
            GroupName = groupName,
            AdminName = "Dono",
            Email = email,
            Password = password,
            ConfirmPassword = password,
        };

    [Fact]
    public async Task CreateGroup_creates_admin_user_group_and_groupadmin_membership()
    {
        using var db = CreateContext();

        await Service(db).CreateGroupAsync(NewGroup(), Ct);

        var group = await db.Groups.SingleAsync(g => g.Slug == "grupo-dos-amigos", Ct);
        Assert.True(group.IsActive);
        Assert.Equal("Grupo dos Amigos", group.Name);

        var admin = await db.Users.SingleAsync(u => u.Email == "owner@x.com", Ct);
        Assert.Equal(UserStatus.Approved, admin.Status);
        Assert.True(admin.IsActive);
        Assert.Equal(admin.Id, group.OwnerUserId);

        var membership = await db.GroupUsers.SingleAsync(gu => gu.GroupId == group.Id && gu.UserId == admin.Id, Ct);
        Assert.Equal(GroupRole.GroupAdmin, membership.Role);
        Assert.Equal(GroupUserStatus.Approved, membership.Status);
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "GroupCreated");
    }

    [Fact]
    public async Task CreateGroup_admin_can_log_in_immediately()
    {
        using var db = CreateContext();
        await Service(db).CreateGroupAsync(NewGroup(email: "login@x.com"), Ct);

        var outcome = await Service(db).LoginAsync(new LoginRequest { Email = "login@x.com", Password = "Senha123" }, Ct);

        Assert.True(outcome.Success);
    }

    [Fact]
    public async Task CreateGroup_generates_unique_slug_for_duplicate_names()
    {
        using var db = CreateContext();
        await Service(db).CreateGroupAsync(NewGroup(groupName: "Família", email: "a@x.com"), Ct);
        await Service(db).CreateGroupAsync(NewGroup(groupName: "Família", email: "b@x.com"), Ct);

        var slugs = await db.Groups.Where(g => g.Name == "Família").Select(g => g.Slug).ToListAsync(Ct);
        Assert.Equal(2, slugs.Distinct().Count());
        Assert.Contains("familia", slugs);
        Assert.Contains("familia-2", slugs);
    }

    [Fact]
    public async Task CreateGroup_rejects_duplicate_email()
    {
        using var db = CreateContext();
        SeedUser(db, UserStatus.Approved, active: true, email: "taken@x.com");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db).CreateGroupAsync(NewGroup(email: "taken@x.com"), Ct));
        Assert.Equal("user.emailExists", ex.Key);
    }

    // --- Login --------------------------------------------------------------

    [Fact]
    public async Task Login_succeeds_for_approved_active_user()
    {
        using var db = CreateContext();
        SeedUser(db, UserStatus.Approved, active: true, email: "ok@x.com");

        var outcome = await Service(db).LoginAsync(new LoginRequest { Email = "ok@x.com", Password = "Senha123" }, Ct);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Response);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Response!.Token));
    }

    [Fact]
    public async Task Login_fails_with_invalid_credentials_for_wrong_password()
    {
        using var db = CreateContext();
        SeedUser(db, UserStatus.Approved, active: true, email: "ok@x.com");

        var outcome = await Service(db).LoginAsync(new LoginRequest { Email = "ok@x.com", Password = "errada" }, Ct);

        Assert.False(outcome.Success);
        Assert.True(outcome.InvalidCredentials);
        Assert.Equal("auth.invalidCredentials", outcome.FailureKey);
    }

    [Theory]
    [InlineData(UserStatus.PendingApproval, false, "auth.pendingApproval")]
    [InlineData(UserStatus.Rejected, false, "auth.rejected")]
    [InlineData(UserStatus.Inactive, false, "auth.accountInactive")]
    [InlineData(UserStatus.Approved, false, "auth.accountInactive")] // approved but deactivated
    public async Task Login_is_blocked_by_account_status(UserStatus status, bool active, string expectedKey)
    {
        using var db = CreateContext();
        SeedUser(db, status, active, email: "blocked@x.com");

        var outcome = await Service(db).LoginAsync(new LoginRequest { Email = "blocked@x.com", Password = "Senha123" }, Ct);

        Assert.False(outcome.Success);
        Assert.False(outcome.InvalidCredentials);
        Assert.Equal(expectedKey, outcome.FailureKey);
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "LoginBlocked");
    }
}
