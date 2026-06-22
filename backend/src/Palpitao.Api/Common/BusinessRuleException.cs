namespace Palpitao.Api.Common;

/// <summary>
/// Thrown when a business/domain rule is violated. Mapped to HTTP 422. The
/// constructor argument is a message key (see <see cref="DomainMessages"/>); the
/// base message is the Portuguese fallback, and the middleware localizes the key
/// to the request language for the response.
/// </summary>
public class BusinessRuleException : Exception
{
    public string Key { get; }

    public BusinessRuleException(string key)
        : base(DomainMessages.Pt(key))
    {
        Key = key;
    }
}
