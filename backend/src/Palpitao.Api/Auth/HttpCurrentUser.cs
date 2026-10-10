using System.Security.Claims;
using Palpitao.Api.Abstractions;
using Palpitao.Domain.Enums;

namespace Palpitao.Api.Auth;

/// <summary><see cref="ICurrentUser"/> read from the HTTP request: the JWT claims and the group header.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    /// <summary>The header the SPA uses to name the group a request acts in.</summary>
    public const string GroupHeader = "X-Group-Id";

    public Guid? UserId
    {
        get
        {
            var value = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsSuperAdmin => http.HttpContext?.User.IsInRole(UserRole.Admin.ToString()) == true;

    public string? RequestedGroupId => http.HttpContext?.Request.Headers[GroupHeader].ToString();
}
