using Palpitao.Api.Common;
using Xunit;

namespace Palpitao.Api.Tests.Validation;

public class PublicKeyGeneratorTests
{
    [Fact]
    public void Generates_twelve_uppercase_hex_characters()
    {
        var key = PublicKeyGenerator.Generate();

        Assert.Equal(PublicKeyGenerator.KeyLength, key.Length);
        Assert.All(key, c => Assert.True(Uri.IsHexDigit(c) && !char.IsLower(c), $"unexpected char '{c}'"));
    }

    [Fact]
    public void Generates_distinct_keys()
    {
        // The unique index is the backstop, but a generator that repeats itself would turn
        // every season creation into a coin flip against a constraint violation.
        var keys = Enumerable.Range(0, 500).Select(_ => PublicKeyGenerator.Generate()).ToHashSet();

        Assert.Equal(500, keys.Count);
    }

    [Fact]
    public void Generated_keys_normalize_to_themselves()
    {
        var key = PublicKeyGenerator.Generate();

        Assert.Equal(key, PublicKeyGenerator.Normalize(key));
    }

    [Theory]
    [InlineData("A7C39F2E4BD8")]
    [InlineData("a7c39f2e4bd8")]
    [InlineData("A7C3-9F2E-4BD8")]
    [InlineData("a7c3-9f2e-4bd8")]
    [InlineData("A7C3 9F2E 4BD8")]
    [InlineData("a7c3_9f2e_4bd8")]
    [InlineData("  A7C3-9F2E-4BD8  ")]
    public void Normalizes_cosmetic_variations_to_the_canonical_key(string input)
        => Assert.Equal("A7C39F2E4BD8", PublicKeyGenerator.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A7C39F2E4BD")]      // too short
    [InlineData("A7C39F2E4BD88")]    // too long
    [InlineData("A7C39F2E4BDZ")]     // Z is not hex
    [InlineData("../../etc/passwd")]
    [InlineData("' OR 1=1 --")]
    public void Rejects_anything_that_is_not_a_key(string? input)
        => Assert.Equal(string.Empty, PublicKeyGenerator.Normalize(input));
}
