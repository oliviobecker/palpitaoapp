using Palpitao.Api.Auth;
using Palpitao.Infrastructure.Identity;
using Palpitao.Application.Auth;
using Xunit;

namespace Palpitao.Api.Tests.Auth;

public class LogRedactionTests
{
    [Theory]
    [InlineData("maria@example.com", "m***@example.com")]
    [InlineData("a@b.co", "a***@b.co")]
    [InlineData("not-an-address", "***")]
    [InlineData("@example.com", "***")]
    [InlineData("", "***")]
    [InlineData(null, "***")]
    public void An_email_keeps_only_its_first_letter_and_domain(string? email, string expected)
    {
        Assert.Equal(expected, LogRedaction.Email(email));
    }
}
