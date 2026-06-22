using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Auth;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtSettings _settings;

    public RefreshTokenService(AppDbContext db, IOptions<JwtSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<(string RawToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, CancellationToken ct)
    {
        var (raw, hash) = GenerateToken();
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays),
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return (raw, token.ExpiresAtUtc);
    }

    public async Task<RefreshRotation> RotateAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return RefreshRotation.Failed();
        }

        var hash = Hash(rawToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
        {
            return RefreshRotation.Failed();
        }

        // Reuse detection: an already-revoked token is being presented again. This
        // is the classic signal of a stolen token, so revoke everything for the user.
        if (existing.RevokedAtUtc is not null)
        {
            await RevokeAllActiveForUserAsync(existing.UserId, ct);
            return RefreshRotation.Failed();
        }

        if (DateTime.UtcNow >= existing.ExpiresAtUtc)
        {
            return RefreshRotation.Failed();
        }

        // The account must still be eligible to authenticate.
        var user = existing.User;
        if (user is null || !user.IsActive || user.Status != UserStatus.Approved)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return RefreshRotation.Failed();
        }

        // Rotate: revoke the presented token and issue a fresh one linked to it.
        var (raw, newHash) = GenerateToken();
        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays),
            CreatedAtUtc = DateTime.UtcNow,
        };
        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.ReplacedByTokenId = replacement.Id;
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        return RefreshRotation.Ok(user, raw, replacement.ExpiresAtUtc);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var hash = Hash(rawToken);
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAtUtc == null, ct);
        if (existing is not null)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>A 256-bit random token and its hex SHA-256 hash (what we store).</summary>
    private static (string Raw, string Hash) GenerateToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw));
    }

    private static string Hash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
