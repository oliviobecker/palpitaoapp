using System.Net;

namespace Palpitao.Api.Common;

/// <summary>
/// Adds bounded retry-with-backoff to the external fixture/results HTTP clients so a
/// single transient blip (timeout, connection reset, 5xx/408/429) doesn't fail a whole
/// import or background refresh. Only idempotent GET/HEAD requests are retried; anything
/// else passes through untouched. Combined with the per-client request timeout, this
/// covers the common transient-failure modes without a circuit breaker dependency.
/// </summary>
public sealed class TransientHttpRetryHandler : DelegatingHandler
{
    private static readonly TimeSpan[] DefaultBackoff =
    {
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(600),
    };

    private readonly ILogger<TransientHttpRetryHandler> _logger;
    private readonly IReadOnlyList<TimeSpan> _backoff;

    /// <param name="backoff">Delay before each retry (one entry per retry). Defaults to
    /// 200 ms + 600 ms (two retries); tests pass near-zero delays.</param>
    public TransientHttpRetryHandler(ILogger<TransientHttpRetryHandler> logger, IReadOnlyList<TimeSpan>? backoff = null)
    {
        _logger = logger;
        _backoff = backoff ?? DefaultBackoff;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Only retry safe, body-less requests (the providers issue GETs). Re-sending a
        // request with a consumed body is unsafe, so non-GET/HEAD passes straight through.
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return await base.SendAsync(request, ct);
        }

        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                // The original message can only be sent once; clone for each retry.
                response = await base.SendAsync(attempt == 0 ? request : Clone(request), ct);
                if (attempt >= _backoff.Count || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                _logger.LogWarning(
                    "Transient HTTP {Status} from {Uri}; retry {Attempt}/{Max}.",
                    (int)response.StatusCode, request.RequestUri, attempt + 1, _backoff.Count);
            }
            catch (HttpRequestException ex) when (attempt < _backoff.Count && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex, "Transient HTTP error from {Uri}; retry {Attempt}/{Max}.",
                    request.RequestUri, attempt + 1, _backoff.Count);
            }

            response?.Dispose();
            await Task.Delay(_backoff[attempt], ct);
        }
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static bool IsTransient(HttpStatusCode status)
        => status == HttpStatusCode.RequestTimeout      // 408
        || status == HttpStatusCode.TooManyRequests     // 429
        || (int)status >= 500;                          // 5xx
}
