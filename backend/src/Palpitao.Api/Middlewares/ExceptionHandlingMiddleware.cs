using System.Text.Json;
using System.Text.Json.Serialization;
using Palpitao.Api.Common;
using Palpitao.Application.Common.Exceptions;
using Palpitao.Domain.Common;
using Palpitao.Api.Localization;
using Palpitao.Application.Abstractions;

namespace Palpitao.Api.Middlewares;

/// <summary>
/// Converts unhandled/domain exceptions into a consistent JSON response whose message is
/// localized to the request's language.
/// </summary>
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
        catch (ValidationException ex)
        {
            _logger.LogInformation("Validation failed: {Key} ({Path})", ex.Key, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status400BadRequest, Localize(context, ex.Key));
        }
        catch (NotFoundException ex)
        {
            _logger.LogInformation("Not found: {Key} ({Path})", ex.Key, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status404NotFound, Localize(context, ex.Key));
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning("Access denied: {Key} ({Path})", ex.Key, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status403Forbidden, Localize(context, ex.Key));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning("Business rule violated: {Key} ({Path})", ex.Key, context.Request.Path);
            await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, Localize(context, ex.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error at {Path}", context.Request.Path);
            var localizer = context.RequestServices.GetRequiredService<ILocalizationService>();
            await WriteProblem(
                context,
                StatusCodes.Status500InternalServerError,
                localizer.Get("error.unexpected"));
        }
    }

    private static string Localize(HttpContext context, string key)
        => context.RequestServices.GetRequiredService<ILocalizationService>().Get(key);

    private static async Task WriteProblem(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        // Always surface the trace id so any error (not just 500s) can be correlated
        // with the server logs / Sentry event when a user reports a problem.
        var payload = JsonSerializer.Serialize(
            new
            {
                status = statusCode,
                message,
                traceId = context.TraceIdentifier,
            },
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });

        await context.Response.WriteAsync(payload);
    }
}
