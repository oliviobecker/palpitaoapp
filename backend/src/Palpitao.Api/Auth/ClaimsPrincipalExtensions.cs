using System.Security.Claims;

namespace Palpitao.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Gets the authenticated user's id from the JWT (never trust the body).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Usuário autenticado inválido.");
    }
}
