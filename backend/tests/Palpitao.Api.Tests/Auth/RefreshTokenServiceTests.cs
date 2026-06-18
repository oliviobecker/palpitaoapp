using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Auth;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Auth;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Auth;

public class RefreshTokenServiceTests
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

    private static RefreshTokenService Service(AppDbContext db, int refreshDays = 30) =>
        new(db, Options.Create(new JwtSettings { RefreshTokenDays = refreshDays }));

    // The seeded development admin is approved + active, so it's a valid token owner.
    private static Guid UserId => SeedIds.AdminUser;

    [Fact]
    public async Task Issue_persists_only_the_hash_not_the_raw_token()
    {
        using var db = CreateContext();
        var (raw, expiresAt) = await Service(db).IssueAsync(UserId, Ct);

        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.True(expiresAt > DateTime.UtcNow);

        var stored = await db.RefreshTokens.SingleAsync(Ct);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Null(stored.RevokedAtUtc);
    }

    [Fact]
    public async Task Rotate_issues_a_new_token_and_revokes_the_old_one()
    {
        using var db = CreateContext();
        var svc = Service(db);
        var (raw, _) = await svc.IssueAsync(UserId, Ct);

        var rotation = await svc.RotateAsync(raw, Ct);

        Assert.True(rotation.Success);
        Assert.NotNull(rotation.User);
        Assert.NotNull(rotation.RawToken);
        Assert.NotEqual(raw, rotation.RawToken);

        // The original is revoked and linked to its replacement.
        var tokens = await db.RefreshTokens.ToListAsync(Ct);
        Assert.Equal(2, tokens.Count);
        var old = tokens.Single(t => t.ReplacedByTokenId != null);
        Assert.NotNull(old.RevokedAtUtc);
        Assert.Contains(tokens, t => t.Id == old.ReplacedByTokenId && t.RevokedAtUtc == null);
    }

    [Fact]
    public async Task Rotate_fails_for_an_unknown_token()
    {
        using var db = CreateContext();
        var rotation = await Service(db).RotateAsync("not-a-real-token", Ct);
        Assert.False(rotation.Success);
    }

    [Fact]
    public async Task Rotate_fails_for_an_expired_token()
    {
        using var db = CreateContext();
        // Issue with a negative lifetime so the token is already expired.
        var (raw, _) = await Service(db, refreshDays: -1).IssueAsync(UserId, Ct);

        var rotation = await Service(db).RotateAsync(raw, Ct);
        Assert.False(rotation.Success);
    }

    [Fact]
    public async Task Rotate_reusing_a_revoked_token_revokes_every_active_token_for_the_user()
    {
        using var db = CreateContext();
        var svc = Service(db);
        var (raw, _) = await svc.IssueAsync(UserId, Ct);

        // First rotation succeeds and produces a still-active replacement.
        var first = await svc.RotateAsync(raw, Ct);
        Assert.True(first.Success);

        // Reusing the original (now revoked) token is treated as theft.
        var reuse = await svc.RotateAsync(raw, Ct);
        Assert.False(reuse.Success);

        // The whole chain — including the otherwise-valid replacement — is revoked.
        var active = await db.RefreshTokens.CountAsync(t => t.RevokedAtUtc == null, Ct);
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task Revoke_prevents_a_later_rotation()
    {
        using var db = CreateContext();
        var svc = Service(db);
        var (raw, _) = await svc.IssueAsync(UserId, Ct);

        await svc.RevokeAsync(raw, Ct);

        var rotation = await svc.RotateAsync(raw, Ct);
        Assert.False(rotation.Success);
    }

    [Fact]
    public async Task Rotate_fails_when_the_account_is_no_longer_active()
    {
        using var db = CreateContext();
        var svc = Service(db);
        var (raw, _) = await svc.IssueAsync(UserId, Ct);

        var user = await db.Users.SingleAsync(u => u.Id == UserId, Ct);
        user.IsActive = false;
        await db.SaveChangesAsync(Ct);

        var rotation = await svc.RotateAsync(raw, Ct);
        Assert.False(rotation.Success);
    }
}
