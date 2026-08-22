using System.Security.Cryptography;

namespace Palpitao.Api.Common;

/// <summary>
/// Mints and normalizes a season's public key — the only credential guarding the public
/// standings link. Twelve uppercase hex characters (16^12, around 2.8e14 combinations),
/// a format that contains none of the ambiguous letters (O, I, L, S), so the key survives
/// being read out loud or copied from a printed sheet. Stored unhyphenated; the UI groups
/// it as XXXX-XXXX-XXXX purely for legibility.
/// </summary>
public static class PublicKeyGenerator
{
    /// <summary>Number of hex characters in a key.</summary>
    public const int KeyLength = 12;

    /// <summary>A fresh cryptographically random key.</summary>
    public static string Generate()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(KeyLength / 2));

    /// <summary>
    /// Canonical form of a key that came from a URL or from someone typing it: hyphens,
    /// spaces and case are cosmetic, so "a7c3-9f2e-4bd8" resolves the same row as
    /// "A7C39F2E4BD8". Anything that is not a well-formed key yields an empty string, which
    /// callers treat as "not found" — never as a wildcard.
    /// </summary>
    public static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[KeyLength];
        var length = 0;

        foreach (var c in key)
        {
            if (c is '-' or ' ' or '_')
            {
                continue;
            }

            if (!Uri.IsHexDigit(c) || length == KeyLength)
            {
                return string.Empty;
            }

            buffer[length++] = char.ToUpperInvariant(c);
        }

        return length == KeyLength ? new string(buffer) : string.Empty;
    }
}
