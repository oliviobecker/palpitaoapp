using Microsoft.AspNetCore.Http;

namespace Palpitao.Api.Services.Groups;

/// <summary>
/// Lightweight, DB-free accessor for the group id carried on the current request's
/// <c>X-Group-Id</c> header. Unlike <see cref="ICurrentGroupService"/> it performs no
/// validation and no database access, so <see cref="Data.AppDbContext"/> can consume it
/// without a dependency cycle.
/// </summary>
/// <remarks>
/// Returns <c>null</c> when there is no HTTP context — background services, data seeding,
/// EF design-time and unit tests — which intentionally disables the multi-tenant query
/// filter and insert-stamping for those non-request paths. Access is still authorized
/// separately by <see cref="ICurrentGroupService"/>; this only scopes the data layer.
/// </remarks>
public interface IRequestGroupContext
{
    /// <summary>The current request's group id, or <c>null</c> outside an HTTP request.</summary>
    Guid? CurrentGroupId { get; }
}

/// <inheritdoc />
public sealed class RequestGroupContext : IRequestGroupContext
{
    private readonly IHttpContextAccessor _http;

    public RequestGroupContext(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid? CurrentGroupId
    {
        get
        {
            var header = _http.HttpContext?.Request.Headers[CurrentGroupService.GroupHeader].ToString();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }
}
