using System.Net;
using System.Net.Http.Json;
using Palpitao.Application.Auth;

namespace Palpitao.IntegrationTests;

/// <summary>The API with only two auth requests per window, to reach the limit quickly.</summary>
public sealed class LowAuthLimitApiFactory : ApiFactory
{
    protected override int AuthPermitLimit => 2;
}

public class RateLimitTests(LowAuthLimitApiFactory api) : IClassFixture<LowAuthLimitApiFactory>
{
    [Fact]
    public async Task Too_many_auth_attempts_are_a_429_problem_with_retry_after()
    {
        var client = api.CreateClient("en-US");
        var attempt = new LoginRequest { Email = "someone@example.com", Password = "wrong-password" };

        await client.PostAsJsonAsync("/auth/login", attempt);
        await client.PostAsJsonAsync("/auth/login", attempt);
        var rejected = await client.PostAsJsonAsync("/auth/login", attempt);

        await ProblemAssert.IsProblemAsync(
            rejected, HttpStatusCode.TooManyRequests,
            "Too many attempts in a short time. Please wait a moment and try again.");
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }
}
