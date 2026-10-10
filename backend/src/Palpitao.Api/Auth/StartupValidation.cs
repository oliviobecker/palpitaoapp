using System.Text;

namespace Palpitao.Api.Auth;

/// <summary>
/// Fail-fast checks for security-critical configuration, run once at startup.
/// A missing/weak JWT signing key or an absent connection string should crash the
/// app loudly rather than let it boot into an insecure or broken state.
/// </summary>
public static class StartupValidation
{
    /// <summary>The placeholder key shipped in appsettings.json for local dev only.</summary>
    public const string DevelopmentPlaceholderJwtKey =
        "dev-only-change-me-to-a-long-random-secret-of-at-least-32-bytes";

    /// <summary>Minimum signing-key length: HMAC-SHA256 needs at least 256 bits (32 bytes).</summary>
    public const int MinimumJwtKeyBytes = 32;

    /// <summary>Validates all security-critical configuration. Throws on the first problem.</summary>
    public static void Validate(string? jwtKey, string? connectionString, bool isDevelopment)
    {
        ValidateConnectionString(connectionString);
        ValidateJwtKey(jwtKey, isDevelopment);
    }

    /// <summary>Requires a non-empty database connection string.</summary>
    public static void ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. "
                + "Set it via the ConnectionStrings__DefaultConnection environment variable.");
        }
    }

    /// <summary>
    /// Requires a JWT signing key that is present and at least <see cref="MinimumJwtKeyBytes"/>
    /// bytes long. Outside Development the known dev placeholder is also rejected, so a
    /// deploy that forgot to override the key fails fast instead of accepting forgeable tokens.
    /// </summary>
    public static void ValidateJwtKey(string? jwtKey, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Generate a random secret of at least "
                + $"{MinimumJwtKeyBytes} bytes and set it via the Jwt__Key environment variable.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(jwtKey);
        if (byteCount < MinimumJwtKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key is too short ({byteCount} bytes). HMAC-SHA256 requires a key of at least "
                + $"{MinimumJwtKeyBytes} bytes. Generate a longer random secret.");
        }

        if (!isDevelopment && jwtKey == DevelopmentPlaceholderJwtKey)
        {
            throw new InvalidOperationException(
                "Jwt:Key is still the development placeholder. Set a unique, random secret via the "
                + "Jwt__Key environment variable before deploying outside Development.");
        }
    }
}
