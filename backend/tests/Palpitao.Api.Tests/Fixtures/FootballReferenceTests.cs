using Palpitao.Api.Common;
using Xunit;

namespace Palpitao.Api.Tests.Fixtures;

public class FootballReferenceTests
{
    [Theory]
    [InlineData("Wolves", "wolverhampton wanderers")]
    [InlineData("Newcastle United", "newcastle")]
    [InlineData("Tottenham Hotspur", "tottenham")]
    [InlineData("Spurs", "tottenham")]
    [InlineData("Brighton", "brighton & hove albion")]
    [InlineData("Nott'm Forest", "nottingham forest")]
    [InlineData("MK Dons", "milton keynes dons")]
    [InlineData("  Man   Utd  ", "manchester united")]
    public void Canonical_resolves_external_name_variants(string input, string expected)
        => Assert.Equal(expected, FootballReference.Canonical(input));

    [Theory]
    [InlineData("Arsenal", "arsenal")]
    [InlineData("Bristol City", "bristol city")]
    [InlineData("Crewe Alexandra", "crewe alexandra")]
    [InlineData("", "")]
    public void Canonical_leaves_unknown_names_normalized_only(string input, string expected)
        => Assert.Equal(expected, FootballReference.Canonical(input));

    /// <summary>
    /// The alias map must never chain (no value is also a key), so Canonical is
    /// idempotent and safe to apply to both sides of a comparison — which is exactly
    /// how the import and results matchers use it.
    /// </summary>
    [Fact]
    public void Canonical_is_idempotent()
    {
        foreach (var (alias, canonical) in FootballReference.Aliases)
        {
            Assert.False(
                FootballReference.Aliases.ContainsKey(canonical),
                $"Alias '{alias}' resolves to '{canonical}', which is itself an alias key — the map would chain.");
            Assert.Equal(canonical, FootballReference.Canonical(canonical));
        }
    }

    /// <summary>Keys must already be normalized, otherwise the lookup can never hit.</summary>
    [Fact]
    public void Alias_keys_are_normalized()
    {
        foreach (var alias in FootballReference.Aliases.Keys)
        {
            Assert.Equal(FootballReference.Normalize(alias), alias);
        }
    }
}
