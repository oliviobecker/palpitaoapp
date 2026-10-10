using System.Net;
using System.Net.Http.Json;
using Palpitao.Application.Auth;
using Palpitao.Application.Groups;

namespace Palpitao.IntegrationTests;

/// <summary>
/// Every failure is the same RFC 7807 problem, localized by Accept-Language — whichever layer
/// produced it (a validator, a service, the rate limiter).
/// </summary>
public class ErrorContractTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("en-US", "The e-mail is required.")]
    [InlineData("pt-BR", "O e-mail é obrigatório.")]
    public async Task A_validation_failure_is_a_400_in_the_caller_s_language(string language, string message)
    {
        var response = await api.CreateClient(language).PostAsJsonAsync("/auth/login", new LoginRequest());

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, message);
    }

    [Fact]
    public async Task A_missing_resource_is_a_404()
    {
        var response = await api.CreateClient("en-US").GetAsync("/public/seasons/no-such-key/standings");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "Season not found.");
    }

    [Fact]
    public async Task A_broken_business_rule_is_a_422()
    {
        var response = await api.CreateClient("en-US").PostAsJsonAsync("/auth/create-group", new CreateGroupRequest
        {
            GroupName = "Another pool",
            AdminName = "Someone",
            Email = ApiFactory.AdminEmail, // already taken
            Password = "Str0ng!pass",
            ConfirmPassword = "Str0ng!pass",
        });

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.UnprocessableEntity, "A user with this e-mail already exists.");
    }

    [Fact]
    public async Task Each_problem_carries_its_own_trace_id()
    {
        var client = api.CreateClient("en-US");

        var first = await ProblemAssert.IsProblemAsync(
            await client.GetAsync("/public/seasons/missing-1/standings"), HttpStatusCode.NotFound, "Season not found.");
        var second = await ProblemAssert.IsProblemAsync(
            await client.GetAsync("/public/seasons/missing-2/standings"), HttpStatusCode.NotFound, "Season not found.");

        Assert.NotEqual(first.GetProperty("traceId").GetString(), second.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Expected_failures_are_not_logged_as_errors()
    {
        // An Error log line is what Sentry turns into an event: a 400, 404 or 422 is the API
        // working as designed, so none of them may write one.
        var client = api.CreateClient("en-US");
        var before = api.ErrorLogs.Lines.Count;

        await client.PostAsJsonAsync("/auth/login", new LoginRequest());
        await client.GetAsync("/public/seasons/no-such-key/standings");
        await client.PostAsJsonAsync("/auth/create-group", new CreateGroupRequest
        {
            GroupName = "Another pool",
            AdminName = "Someone",
            Email = ApiFactory.AdminEmail,
            Password = "Str0ng!pass",
            ConfirmPassword = "Str0ng!pass",
        });

        Assert.Empty(api.ErrorLogs.Lines.Skip(before));
    }
}
