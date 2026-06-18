using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Auth;

/// <summary>Result of rotating a refresh token. On success carries the user and the
/// freshly issued raw token; on failure the caller should treat the session as ended.</summary>
public record RefreshRotation(bool Success, User? User, string? RawToken, DateTime ExpiresAtUtc)
{
    public static RefreshRotation Failed() => new(false, null, null, default);
    public static RefreshRotation Ok(User user, string rawToken, DateTime expiresAtUtc)
        => new(true, user, rawToken, expiresAtUtc);
}

/// <summary>
/// Lifecycle for rotating refresh tokens: issue on login, rotate-on-use (with reuse
/// detection), and revoke on logout. Only token hashes are persisted.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token for the user and returns the raw value (shown once).</summary>
    Task<(string RawToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Validates and rotates the presented token: the old token is revoked and a new
    /// one issued. An expired/unknown token fails; re-presenting an already-revoked
    /// token is treated as theft and revokes every active token for that user.
    /// </summary>
    Task<RefreshRotation> RotateAsync(string rawToken, CancellationToken ct);

    /// <summary>Revokes the presented token if it is still active (logout). No-op otherwise.</summary>
    Task RevokeAsync(string rawToken, CancellationToken ct);
}
