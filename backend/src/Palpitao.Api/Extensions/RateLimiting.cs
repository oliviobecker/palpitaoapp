using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Palpitao.Application.Abstractions;

namespace Palpitao.Api.Extensions;

/// <summary>The rate-limit policy names the controllers opt into.</summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// The unauthenticated auth endpoints (login/register/create-group/refresh), per client IP, so
    /// BCrypt's per-guess cost can't be brute-forced by volume.
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// The public standings link: unauthenticated, so per IP like the auth endpoints — but generous,
    /// since it is a cheap read and a whole group opens the same link at once when a round ends.
    /// </summary>
    public const string Public = "public";

    /// <summary>
    /// OCR import: CPU-bound (Tesseract) with up-to-10MB uploads, so throttled per admin. Sized for
    /// a whole round sent at once (the multi-image upload sends one image at a time, so the CPU
    /// still reads one screenshot per admin at a time): a round has been up to 18 screenshots.
    /// </summary>
    public const string Ocr = "ocr";

    /// <summary>
    /// Serving stored OCR images: a cheap read, but a gallery issues many of them — the import
    /// throttle would reject the second row of cards.
    /// </summary>
    public const string OcrImage = "ocrImage";
}

public static class RateLimitingExtensions
{
    /// <summary>
    /// Fixed-window limits per policy, each tunable under <c>RateLimiting:&lt;Name&gt;</c>
    /// (<c>PermitLimit</c>, <c>WindowSeconds</c>). Behind a reverse proxy, configure forwarded
    /// headers at the proxy so <c>RemoteIpAddress</c> is the real client, not the proxy.
    /// </summary>
    public static IServiceCollection AddRateLimitPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindow(RateLimitPolicies.Auth, configuration.GetSection("RateLimiting:Auth"), 20, 60, ByClientIp);
            options.AddFixedWindow(RateLimitPolicies.Public, configuration.GetSection("RateLimiting:Public"), 60, 60, ByClientIp);
            // Authenticated endpoints: partition by user id (falls back to IP pre-auth).
            options.AddFixedWindow(RateLimitPolicies.Ocr, configuration.GetSection("RateLimiting:Ocr"), 20, 60, ByUserOrClientIp);
            options.AddFixedWindow(RateLimitPolicies.OcrImage, configuration.GetSection("RateLimiting:OcrImage"), 60, 60, ByUserOrClientIp);
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    private static void AddFixedWindow(
        this RateLimiterOptions options,
        string policy,
        IConfigurationSection section,
        int defaultPermitLimit,
        int defaultWindowSeconds,
        Func<HttpContext, string> partitionKey)
    {
        var permitLimit = section.GetValue<int?>("PermitLimit") ?? defaultPermitLimit;
        var windowSeconds = section.GetValue<int?>("WindowSeconds") ?? defaultWindowSeconds;
        options.AddPolicy(policy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                }));
    }

    private static string ByClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string ByUserOrClientIp(HttpContext httpContext) =>
        httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ByClientIp(httpContext);

    // The same problem-details body as every other error, localized.
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        if (!response.HasStarted)
        {
            var localizer = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            await context.HttpContext.WriteProblemAsync(
                StatusCodes.Status429TooManyRequests, localizer.Get("error.tooManyRequests"));
        }
    }
}
