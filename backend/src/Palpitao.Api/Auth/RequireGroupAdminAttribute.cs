using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Auth;

/// <summary>
/// Action filter that requires the authenticated user to be an approved
/// <c>GroupAdmin</c> of the current group (from the <c>X-Group-Id</c> header).
/// Throws <see cref="Common.ForbiddenException"/> (HTTP 403) otherwise. Combine
/// with <c>[Authorize]</c> so the user is authenticated first.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireGroupAdminAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var current = context.HttpContext.RequestServices.GetRequiredService<ICurrentGroupService>();
        await current.RequireGroupAdminAsync(context.HttpContext.RequestAborted);
        await next();
    }
}
