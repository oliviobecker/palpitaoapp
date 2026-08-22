using Palpitao.Api.Common;
using Palpitao.Api.Services.Localization;

namespace Palpitao.Api.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="ILocalizationService"/> that resolves against the real
/// <see cref="DomainMessages"/> catalogue in a fixed language, without an HTTP context.
/// </summary>
public sealed class FakeLocalizationService : ILocalizationService
{
    public FakeLocalizationService(string language = "en")
    {
        Language = language;
    }

    public string Language { get; }

    public string Get(string key) => DomainMessages.Resolve(key, Language);

    public string Get(string key, string? acceptLanguage)
        => DomainMessages.Resolve(key, ResolveLanguage(acceptLanguage));

    public string ResolveLanguage(string? acceptLanguage)
        => acceptLanguage?.TrimStart().StartsWith("pt", StringComparison.OrdinalIgnoreCase) == true ? "pt" : "en";
}
