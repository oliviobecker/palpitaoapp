using Palpitao.Api.Extensions;
using Palpitao.Application.Abstractions;
using Palpitao.Application.Common.Exceptions;
using Palpitao.Domain.Common;

namespace Palpitao.Api.Middlewares;

/// <summary>
/// Turns exceptions into RFC 7807 problem details whose <c>detail</c> (and <c>message</c>, see
/// <see cref="ProblemDetailsExtensions"/>) is the error key localized to the request's
/// language: the domain's exceptions become 400/403/404/422, anything else a 500 that reveals
/// nothing about its cause.
/// </summary>
/// <remarks>
/// A middleware rather than an <c>IExceptionHandler</c> on purpose: exceptions handled through
/// <c>UseExceptionHandler</c> are surfaced on <c>IExceptionHandlerFeature</c>, where Sentry's
/// middleware captures them — every expected 404 or 422 would become a Sentry event. Caught here,
/// only the unexpected ones reach Sentry, through their error log.
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away: nobody is waiting for an answer, and it is not a server error.
            _logger.LogDebug("Request aborted by the client ({Path}).", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception ex) when (Expected(ex) is { } expected)
        {
            var (status, level, reason, key) = expected;
            _logger.Log(level, "{Reason}: {Key} ({Path})", reason, key, context.Request.Path);
            await WriteProblemAsync(context, status, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error at {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "error.unexpected");
        }
    }

    /// <summary>The domain's own failures: their status, how loudly to log them, and the message key.</summary>
    private static (int Status, LogLevel Level, string Reason, string Key)? Expected(Exception ex) => ex switch
    {
        ValidationException e => (StatusCodes.Status400BadRequest, LogLevel.Information, "Validation failed", e.Key),
        NotFoundException e => (StatusCodes.Status404NotFound, LogLevel.Information, "Not found", e.Key),
        ForbiddenException e => (StatusCodes.Status403Forbidden, LogLevel.Warning, "Access denied", e.Key),
        BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, LogLevel.Warning, "Business rule violated", e.Key),
        _ => null,
    };

    private static Task WriteProblemAsync(HttpContext context, int status, string key)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        var detail = context.RequestServices.GetRequiredService<ILocalizationService>().Get(key);
        return context.WriteProblemAsync(status, detail);
    }
}
