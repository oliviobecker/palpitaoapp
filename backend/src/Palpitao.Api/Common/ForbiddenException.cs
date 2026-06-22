namespace Palpitao.Api.Common;

/// <summary>
/// Thrown when an authenticated user lacks access to the requested group/resource
/// (e.g. missing/invalid <c>X-Group-Id</c> header, not an approved member, or not
/// a group admin). Mapped to HTTP 403. The constructor argument is a message key
/// (see <see cref="DomainMessages"/>); the middleware localizes it for the
/// response.
/// </summary>
public class ForbiddenException : Exception
{
    public string Key { get; }

    public ForbiddenException(string key = "group.accessDenied")
        : base(DomainMessages.Pt(key))
    {
        Key = key;
    }
}
