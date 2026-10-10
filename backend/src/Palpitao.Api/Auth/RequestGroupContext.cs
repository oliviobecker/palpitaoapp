using Palpitao.Application.Abstractions;
using Palpitao.Application.Auth;
using Palpitao.Infrastructure.Identity;

namespace Palpitao.Api.Auth;

/// <summary>
/// <see cref="IRequestGroupContext"/> read from the request's <c>X-Group-Id</c> header, unless the
/// endpoint is marked <see cref="IgnoreRequestGroupAttribute"/>.
/// </summary>
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
            var http = _http.HttpContext;
            if (http is null)
            {
                return null;
            }

            // Key-addressed public endpoints resolve their tenant from the route. A browser
            // with a session attaches X-Group-Id to every request, including those — and a
            // header naming another group would silently filter their data away.
            if (http.GetEndpoint()?.Metadata.GetMetadata<IgnoreRequestGroupAttribute>() is not null)
            {
                return null;
            }

            var header = http.Request.Headers[HttpCurrentUser.GroupHeader].ToString();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }
}
