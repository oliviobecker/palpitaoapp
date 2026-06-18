namespace Palpitao.Api.Common;

/// <summary>
/// Thrown when a requested resource does not exist. Mapped to HTTP 404. The
/// constructor argument is a message key (see <see cref="DomainMessages"/>); the
/// base message is the Portuguese fallback, and the middleware localizes the key
/// to the request language for the response.
/// </summary>
public class NotFoundException : Exception
{
    public string Key { get; }

    public NotFoundException(string key)
        : base(DomainMessages.Pt(key))
    {
        Key = key;
    }
}
