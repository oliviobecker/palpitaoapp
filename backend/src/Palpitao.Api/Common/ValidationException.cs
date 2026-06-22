namespace Palpitao.Api.Common;

/// <summary>
/// Thrown when request input fails validation (FluentValidation). Mapped to HTTP
/// 400. The constructor argument is a message key (see <see cref="DomainMessages"/>);
/// the base message is the Portuguese fallback, and the middleware localizes the
/// key to the request language (Accept-Language) for the response.
/// </summary>
public class ValidationException : Exception
{
    public string Key { get; }

    public ValidationException(string key)
        : base(DomainMessages.Pt(key))
    {
        Key = key;
    }
}
