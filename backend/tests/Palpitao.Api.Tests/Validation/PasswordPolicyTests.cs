using Palpitao.Api.Common;
using Xunit;

namespace Palpitao.Api.Tests.Validation;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Senha123")]   // 8 chars, letters + digits
    [InlineData("abcd1234")]
    [InlineData("aaaaaaa1")]
    [InlineData("P@ssw0rd")]
    public void Accepts_strong_passwords(string password)
        => Assert.True(PasswordPolicy.IsStrong(password));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Abc123")]      // too short (< 8)
    [InlineData("alphabetic")]  // no digit
    [InlineData("12345678")]    // no letter
    public void Rejects_weak_passwords(string? password)
        => Assert.False(PasswordPolicy.IsStrong(password));

    [Fact]
    public void Validate_throws_on_a_weak_password()
        => Assert.Throws<BusinessRuleException>(() => PasswordPolicy.Validate("weak"));

    [Fact]
    public void Validate_passes_on_a_strong_password()
        => PasswordPolicy.Validate("Senha123");
}
