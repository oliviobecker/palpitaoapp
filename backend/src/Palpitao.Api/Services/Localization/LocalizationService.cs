using Palpitao.Api.Common;

namespace Palpitao.Api.Services.Localization;

public class LocalizationService : ILocalizationService
{
    private readonly IHttpContextAccessor _http;

    public LocalizationService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string Language => ResolveLanguage(AcceptLanguage());

    public string Get(string key) => Get(key, AcceptLanguage());

    public string Get(string key, string? acceptLanguage)
        => DomainMessages.Resolve(key, ResolveLanguage(acceptLanguage));

    public string ResolveLanguage(string? acceptLanguage)
    {
        var value = acceptLanguage?.TrimStart();
        if (!string.IsNullOrEmpty(value) && value.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return "pt";
        }
        return "en"; // English is the fallback.
    }

    private string? AcceptLanguage() => _http.HttpContext?.Request.Headers.AcceptLanguage.ToString();
}
