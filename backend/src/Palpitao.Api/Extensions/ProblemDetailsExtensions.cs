using Microsoft.AspNetCore.WebUtilities;

namespace Palpitao.Api.Extensions;

public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Every error response is an RFC 7807 problem (<c>application/problem+json</c>): <c>type</c>,
    /// <c>title</c>, <c>status</c>, and <c>detail</c> — the message localized to the request's
    /// language. Two members are added to each one: <c>traceId</c>, the request's identifier, so a
    /// reported error can be found in the logs and in Sentry; and <c>message</c>, the same text as
    /// <c>detail</c>, which is what the SPA has always read (see ADR 0007).
    /// </summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            // The framework has no default title/type for a few statuses (429 among them).
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;
            problem.Title ??= ReasonPhrases.GetReasonPhrase(status);
            problem.Type ??= status == StatusCodes.Status429TooManyRequests
                ? "https://www.rfc-editor.org/rfc/rfc6585#section-4"
                : "about:blank";

            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (problem.Detail is { } detail)
            {
                problem.Extensions["message"] = detail;
            }
        });

    /// <summary>
    /// Writes a problem with the given status and localized <paramref name="detail"/> outside an
    /// action (the exception middleware, the rate limiter). Goes through the problem-details
    /// service, so the members above are added; a client that accepts no JSON at all still gets
    /// the problem, as plain JSON.
    /// </summary>
    public static Task WriteProblemAsync(this HttpContext context, int status, string detail) =>
        TypedResults.Problem(detail: detail, statusCode: status).ExecuteAsync(context);
}
