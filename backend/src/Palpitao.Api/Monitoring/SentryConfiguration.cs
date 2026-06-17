using Sentry.AspNetCore;

namespace Palpitao.Api.Monitoring;

public static class SentryConfiguration
{
    public static IWebHostBuilder UsePalpitaoSentry(this IWebHostBuilder builder)
    {
        return builder.UseSentry((context, options) =>
        {
            var settings = LoadSettings(context.Configuration);

            options.Dsn = settings.Dsn;
            options.Environment = settings.Environment;
            options.Release = settings.Release;
            options.Debug = settings.Debug;
            options.SendDefaultPii = false;
            options.TracesSampleRate = settings.TracesSampleRate;
            options.MinimumBreadcrumbLevel = ParseLogLevel(settings.MinimumBreadcrumbLevel, LogLevel.Information);
            options.MinimumEventLevel = ParseLogLevel(settings.MinimumEventLevel, LogLevel.Error);
            options.SetBeforeSend((sentryEvent, _) => SentrySensitiveDataSanitizer.Sanitize(sentryEvent));
        });
    }

    private static SentrySettings LoadSettings(IConfiguration configuration)
    {
        var settings = configuration.GetSection("Sentry").Get<SentrySettings>() ?? new SentrySettings();

        settings.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? settings.Dsn;
        settings.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? settings.Environment;
        settings.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? settings.Release;

        if (double.TryParse(Environment.GetEnvironmentVariable("SENTRY_TRACES_SAMPLE_RATE"), out var tracesSampleRate))
        {
            settings.TracesSampleRate = tracesSampleRate;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("SENTRY_DEBUG"), out var debug))
        {
            settings.Debug = debug;
        }

        return settings;
    }

    private static LogLevel ParseLogLevel(string value, LogLevel fallback)
        => Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : fallback;
}
