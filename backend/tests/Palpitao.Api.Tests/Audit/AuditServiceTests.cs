using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Audit;

/// <summary>
/// An entry written inside a group request takes the group that request already validated, so
/// it reaches that group's Admin → Audit list. The raw <c>X-Group-Id</c> header is never
/// trusted: a request that did not (or could not) resolve its group writes a global entry.
/// </summary>
/// <remarks>
/// <see cref="HttpContextAccessor"/> keeps its context in a static <c>AsyncLocal</c>, so each
/// test builds at most one request — a second accessor would replace the first one's context.
/// </remarks>
public class AuditServiceTests
{
    private static readonly Guid OtherGroup = Guid.Parse("88888888-8888-8888-8888-888888888801");
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

    /// <summary>Seeds a user with an approved, active membership in the default group only.</summary>
    private static Guid SeedMember(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Name = "U", Email = "u@x.com", PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        TestSeed.AddDefaultGroupMembership(db, id);
        db.SaveChanges();
        return id;
    }

    /// <summary>A signed-in request from <paramref name="userId"/> naming <paramref name="groupHeader"/>.</summary>
    private static DefaultHttpContext Request(Guid userId, Guid groupHeader)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        ctx.Request.Headers[CurrentGroupService.GroupHeader] = groupHeader.ToString();
        return ctx;
    }

    private static CurrentGroupService CurrentGroup(AppDbContext db, HttpContext? request)
        => new(db, new HttpContextAccessor { HttpContext = request });

    /// <summary>The stored row for <paramref name="action"/>, read back from the database.</summary>
    private static AuditLog Entry(AppDbContext db, string action)
        => db.AuditLogs.AsNoTracking().Single(a => a.Action == action);

    [Fact]
    public async Task Entry_written_once_the_request_resolved_its_group_is_stamped_and_listed_for_it()
    {
        using var db = CreateContext();
        var userId = SeedMember(db);
        var current = CurrentGroup(db, Request(userId, SeedIds.DefaultGroup));
        var audit = new AuditService(db, current);

        // What [RequireGroupAdmin]/[RequireGroupParticipant] do before any action runs.
        await current.GetGroupIdAsync(Ct);
        audit.Add(userId, "OcrImportConfirmed", nameof(OcrImportBatch), Guid.NewGuid().ToString());
        await db.SaveChangesAsync(Ct);

        Assert.Equal(SeedIds.DefaultGroup, Entry(db, "OcrImportConfirmed").GroupId);
        var listed = await audit.QueryAsync(null, null, null, null, Ct, SeedIds.DefaultGroup);
        Assert.Equal("OcrImportConfirmed", Assert.Single(listed).Action);
    }

    [Fact]
    public async Task Entry_written_before_the_request_resolved_its_group_stays_global()
    {
        using var db = CreateContext();
        var userId = SeedMember(db);
        // The header is there, but nothing on this request resolved it (e.g. login).
        var current = CurrentGroup(db, Request(userId, SeedIds.DefaultGroup));

        new AuditService(db, current).Add(userId, "LoginBlocked", nameof(User), userId.ToString());
        await db.SaveChangesAsync(Ct);

        Assert.Null(Entry(db, "LoginBlocked").GroupId);
    }

    [Fact]
    public async Task Entry_written_outside_a_request_stays_global()
    {
        using var db = CreateContext();
        // A background job: no HTTP context at all.
        var current = CurrentGroup(db, request: null);

        new AuditService(db, current).Add(null, "ResultsRefreshed", nameof(Round), Guid.NewGuid().ToString());
        await db.SaveChangesAsync(Ct);

        Assert.Null(current.ResolvedGroupId);
        Assert.Null(Entry(db, "ResultsRefreshed").GroupId);
    }

    [Fact]
    public async Task Explicit_group_wins_over_the_resolved_one()
    {
        using var db = CreateContext();
        var userId = SeedMember(db);
        var current = CurrentGroup(db, Request(userId, SeedIds.DefaultGroup));
        await current.GetGroupIdAsync(Ct);

        new AuditService(db, current).Add(
            userId, "RegistrationApproved", nameof(GroupUser), Guid.NewGuid().ToString(), groupId: OtherGroup);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(OtherGroup, Entry(db, "RegistrationApproved").GroupId);
    }

    [Fact]
    public async Task Header_naming_a_group_the_user_is_not_a_member_of_is_never_stamped()
    {
        using var db = CreateContext();
        var userId = SeedMember(db);
        var current = CurrentGroup(db, Request(userId, OtherGroup));

        await Assert.ThrowsAsync<ForbiddenException>(() => current.GetGroupIdAsync(Ct));
        Assert.Null(current.ResolvedGroupId);

        new AuditService(db, current).Add(userId, "OcrCandidateDeleted", nameof(OcrImportBatch), Guid.NewGuid().ToString());
        await db.SaveChangesAsync(Ct);

        Assert.Null(Entry(db, "OcrCandidateDeleted").GroupId);
        Assert.Empty(await new AuditService(db).QueryAsync(null, null, null, null, Ct, OtherGroup));
    }

    [Fact]
    public async Task Container_gives_the_audit_service_the_request_scoped_group_service()
    {
        using var db = CreateContext();
        var userId = SeedMember(db);
        // The lifetimes Program.cs registers: both scoped, so the audit service reads the very
        // membership the group filter resolved earlier on the same request.
        using var provider = new ServiceCollection()
            .AddHttpContextAccessor()
            .AddSingleton(db)
            .AddScoped<ICurrentGroupService, CurrentGroupService>()
            .AddScoped<IAuditService, AuditService>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            Request(userId, SeedIds.DefaultGroup);

        await scope.ServiceProvider.GetRequiredService<ICurrentGroupService>().RequireApprovedMemberAsync(Ct);
        scope.ServiceProvider.GetRequiredService<IAuditService>()
            .Add(userId, "RoundScored", nameof(Round), Guid.NewGuid().ToString());
        await db.SaveChangesAsync(Ct);

        Assert.Equal(SeedIds.DefaultGroup, Entry(db, "RoundScored").GroupId);
    }
}
