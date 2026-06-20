using System.Text.RegularExpressions;

namespace Palpitao.Api.Common;

/// <summary>
/// Single source of truth for the account password rule (at least 8 characters with at
/// least one letter and one digit). Shared by the public auth flows and the admin
/// participant-creation flow so every path that mints a password enforces the same
/// strength. Mirrors the frontend validator.
/// </summary>
public static partial class PasswordPolicy
{
    [GeneratedRegex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")]
    private static partial Regex StrongPassword();

    /// <summary>True when the password meets the strength rule.</summary>
    public static bool IsStrong(string? password)
        => !string.IsNullOrEmpty(password) && StrongPassword().IsMatch(password);

    /// <summary>Throws <see cref="BusinessRuleException"/> (<c>auth.weakPassword</c>) when weak.</summary>
    public static void Validate(string? password)
    {
        if (!IsStrong(password))
        {
            throw new BusinessRuleException("auth.weakPassword");
        }
    }
}
