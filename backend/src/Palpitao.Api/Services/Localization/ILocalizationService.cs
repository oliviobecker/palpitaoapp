namespace Palpitao.Api.Services.Localization;

/// <summary>
/// Centralizes API messages in pt/en. Language is resolved from the request's
/// Accept-Language header (pt* -> Portuguese, otherwise English fallback).
/// </summary>
public interface ILocalizationService
{
    /// <summary>Resolved language for the current request ("pt" or "en").</summary>
    string Language { get; }

    /// <summary>Localized message for the current request's language.</summary>
    string Get(string key);

    /// <summary>Localized message for a given Accept-Language value.</summary>
    string Get(string key, string? acceptLanguage);

    /// <summary>Resolves "pt" or "en" from an Accept-Language value.</summary>
    string ResolveLanguage(string? acceptLanguage);
}
