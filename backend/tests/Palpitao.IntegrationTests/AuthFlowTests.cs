using System.Net;
using System.Net.Http.Json;
using Palpitao.Application.Auth;

namespace Palpitao.IntegrationTests;

public class AuthFlowTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static LoginRequest Admin => new() { Email = ApiFactory.AdminEmail, Password = ApiFactory.AdminPassword };

    [Fact]
    public async Task Login_then_refresh_rotates_and_logout_revokes()
    {
        var client = api.CreateClient("en-US");

        var login = await client.PostAsJsonAsync("/auth/login", Admin);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var first = (await login.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json))!;
        Assert.False(string.IsNullOrEmpty(first.Token));

        // A refresh hands out a new pair...
        var refresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest { RefreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var second = (await refresh.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.Json))!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // ...and the token it consumed is spent.
        var reused = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest { RefreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal("application/problem+json", reused.Content.Headers.ContentType?.MediaType);

        // Logging out revokes the current one.
        var logout = await client.PostAsJsonAsync("/auth/logout", new LogoutRequest { RefreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var afterLogout = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest { RefreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_is_a_localized_401_problem()
    {
        var response = await api.CreateClient("en-US")
            .PostAsJsonAsync("/auth/login", new LoginRequest { Email = ApiFactory.AdminEmail, Password = "not-the-password" });

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.Unauthorized, "Invalid e-mail or password.");
    }

    [Fact]
    public async Task A_protected_endpoint_needs_a_token()
    {
        var response = await api.CreateClient().GetAsync("/seasons");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
