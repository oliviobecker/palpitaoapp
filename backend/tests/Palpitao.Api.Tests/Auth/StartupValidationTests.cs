using Palpitao.Api.Auth;
using Xunit;

namespace Palpitao.Api.Tests.Auth;

public class StartupValidationTests
{
    private const string ValidKey = "this-is-a-sufficiently-long-signing-key-32+";

    // --- Connection string ---------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateConnectionString_throws_when_missing(string? connectionString)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidateConnectionString(connectionString));
        Assert.Contains("ConnectionStrings:DefaultConnection", ex.Message);
    }

    [Fact]
    public void ValidateConnectionString_passes_when_present()
    {
        StartupValidation.ValidateConnectionString("Host=db;Database=palpitao;Username=u;Password=p");
    }

    // --- JWT key -------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateJwtKey_throws_when_missing(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidateJwtKey(key, isDevelopment: true));
        Assert.Contains("Jwt:Key", ex.Message);
    }

    [Fact]
    public void ValidateJwtKey_throws_when_shorter_than_32_bytes()
    {
        var shortKey = new string('a', StartupValidation.MinimumJwtKeyBytes - 1);
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidateJwtKey(shortKey, isDevelopment: true));
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void ValidateJwtKey_passes_for_a_long_random_key()
    {
        StartupValidation.ValidateJwtKey(ValidKey, isDevelopment: false);
        StartupValidation.ValidateJwtKey(ValidKey, isDevelopment: true);
    }

    [Fact]
    public void ValidateJwtKey_rejects_the_dev_placeholder_outside_development()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidateJwtKey(
                StartupValidation.DevelopmentPlaceholderJwtKey, isDevelopment: false));
        Assert.Contains("placeholder", ex.Message);
    }

    [Fact]
    public void ValidateJwtKey_allows_the_dev_placeholder_in_development()
    {
        // The shipped placeholder is long enough, so local dev must keep working.
        StartupValidation.ValidateJwtKey(
            StartupValidation.DevelopmentPlaceholderJwtKey, isDevelopment: true);
    }

    [Fact]
    public void DevelopmentPlaceholderJwtKey_meets_the_minimum_length()
    {
        // Guards the placeholder constant against being shortened below the security baseline.
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(StartupValidation.DevelopmentPlaceholderJwtKey)
                >= StartupValidation.MinimumJwtKeyBytes);
    }

    // --- Combined ------------------------------------------------------------

    [Fact]
    public void Validate_throws_on_the_connection_string_before_the_key()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.Validate(jwtKey: null, connectionString: null, isDevelopment: false));
        Assert.Contains("ConnectionStrings:DefaultConnection", ex.Message);
    }

    [Fact]
    public void Validate_passes_with_valid_configuration()
    {
        StartupValidation.Validate(ValidKey, "Host=db;Database=palpitao", isDevelopment: false);
    }
}
