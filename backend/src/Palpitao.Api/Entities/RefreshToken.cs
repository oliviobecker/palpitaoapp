namespace Palpitao.Api.Entities;

/// <summary>
/// A long-lived refresh token used to mint new access tokens without re-login.
/// Only a SHA-256 hash of the raw token is stored, so a database leak does not
/// expose usable tokens. Tokens rotate on every use: the presented token is
/// revoked and a fresh one is issued, linked via <see cref="ReplacedByTokenId"/>.
/// Re-presenting an already-revoked token signals theft and revokes the chain.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hex SHA-256 of the raw token. The raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Set when the token is rotated away or explicitly revoked (logout).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>The token that replaced this one on rotation (for reuse detection).</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public User? User { get; set; }

    /// <summary>True while the token is neither revoked nor expired.</summary>
    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
