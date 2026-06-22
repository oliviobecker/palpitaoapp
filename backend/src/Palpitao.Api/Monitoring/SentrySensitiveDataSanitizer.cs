using Sentry;

namespace Palpitao.Api.Monitoring;

public static class SentrySensitiveDataSanitizer
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "cookie",
        "set-cookie",
        "password",
        "passwordHash",
        "confirmPassword",
        "token",
        "accessToken",
        "refreshToken",
        "jwt",
        "dsn",
        "connectionString",
        "defaultConnection",
        "uploadedFile",
        "ocrText",
        "fullOcrText",
    };

    public static SentryEvent Sanitize(SentryEvent sentryEvent)
    {
        sentryEvent.ServerName = null;

        if (sentryEvent.Request is not null)
        {
            sentryEvent.Request.Cookies = null;
            sentryEvent.Request.Data = null;
            RemoveSensitiveValues(sentryEvent.Request.Headers);
            RemoveSensitiveValues(sentryEvent.Request.Env);
        }

        return sentryEvent;
    }

    public static bool IsSensitiveKey(string key)
        => SensitiveKeys.Any(sensitive => key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));

    private static void RemoveSensitiveValues(IDictionary<string, string>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var key in values.Keys.Where(IsSensitiveKey).ToList())
        {
            values[key] = "[Filtered]";
        }
    }

}
