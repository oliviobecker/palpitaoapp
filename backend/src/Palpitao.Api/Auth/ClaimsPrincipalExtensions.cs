using System.Security.Claims;
using Palpitao.Application.Auth;
using Palpitao.Infrastructure.Identity;

namespace Palpitao.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Gets the authenticated user's id from the JWT (never trust the body).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated user carries no valid user id.");
    }
}
