using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Sentry;

namespace Palpitao.Api.Middlewares;

public class SentryUserContextMiddleware
{
    private readonly RequestDelegate _next;

    public SentryUserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            var role = principal.FindFirstValue(ClaimTypes.Role);

            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new SentryUser
                {
                    Id = userId,
                    Email = email,
                };

                if (!string.IsNullOrWhiteSpace(role))
                {
                    scope.SetTag("user.role", role);
                }

                // Current group (tenant) the request is acting within. The backend
                // re-validates access; this tag is only for diagnostics/triage.
                var groupId = context.Request.Headers["X-Group-Id"].ToString();
                if (!string.IsNullOrWhiteSpace(groupId))
                {
                    scope.SetTag("group_id", groupId);
                }

                scope.SetTag("trace_id", context.TraceIdentifier);
            });
        }

        await _next(context);
    }
}
