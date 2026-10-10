using System.Net;
using System.Text.Json;

namespace Palpitao.IntegrationTests;

/// <summary>Checks the error contract every failing response shares.</summary>
internal static class ProblemAssert
{
    /// <summary>
    /// An RFC 7807 problem with <paramref name="status"/>, whose <c>detail</c> — and <c>message</c>,
    /// which the SPA reads — is <paramref name="message"/>, and which carries a trace id.
    /// </summary>
    public static async Task<JsonElement> IsProblemAsync(HttpResponseMessage response, HttpStatusCode status, string message)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("type").GetString()));
        Assert.Equal(message, problem.GetProperty("detail").GetString());
        Assert.Equal(message, problem.GetProperty("message").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
