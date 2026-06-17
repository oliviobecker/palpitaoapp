using Microsoft.Extensions.Options;
using Sentry;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Periodically refreshes the results of in-play rounds from the configured
/// provider and re-stamps them for the temporary standings. Disabled by default
/// (<see cref="ResultsRefreshOptions.Enabled"/>); never closes a round. Errors are
/// caught and logged so a failed cycle can never take the host down.
/// </summary>
public class ResultsRefreshBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ResultsRefreshOptions _options;
    private readonly ILogger<ResultsRefreshBackgroundService> _logger;

    public ResultsRefreshBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ResultsRefreshOptions> options,
        ILogger<ResultsRefreshBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Results refresh background service is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
        _logger.LogInformation("Results refresh background service started (every {Minutes} min).", interval.TotalMinutes);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var refresher = scope.ServiceProvider.GetRequiredService<IResultsUpdateService>();
                var updated = await refresher.RefreshAllActiveRoundsAsync(stoppingToken);
                _logger.LogInformation("Background results refresh updated {Count} matches.", updated);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a bad cycle crash the host.
                _logger.LogError(ex, "Background results refresh failed.");
                SentrySdk.CaptureException(ex);
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
