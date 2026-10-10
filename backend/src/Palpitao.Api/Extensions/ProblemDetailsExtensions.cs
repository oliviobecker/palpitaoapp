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
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (context.ProblemDetails.Detail is { } detail)
            {
                context.ProblemDetails.Extensions["message"] = detail;
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
