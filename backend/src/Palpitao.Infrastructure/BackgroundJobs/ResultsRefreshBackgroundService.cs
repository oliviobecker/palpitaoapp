using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Palpitao.Application.Results;

namespace Palpitao.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically refreshes the results of in-play rounds from the configured provider
/// and re-stamps them for the temporary standings. Controlled by
/// <see cref="ResultsRefreshOptions.Enabled"/> (the option defaults to off; the shipped
/// appsettings enables it). Never closes a round. One instance at a time (see
/// <see cref="SingleRunnerJob"/>).
/// </summary>
public sealed class ResultsRefreshBackgroundService : SingleRunnerJob
{
    private readonly ResultsRefreshOptions _options;
    private readonly ILogger<ResultsRefreshBackgroundService> _logger;

    public ResultsRefreshBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ResultsRefreshOptions> options,
        ILogger<ResultsRefreshBackgroundService> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Arbitrary, stable application-defined key for the refresh advisory lock.
    protected override long LockKey => 4_125_990_001L;

    protected override string Name => "results refresh";

    protected override TimeSpan? ResolveInterval()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Results refresh background service is disabled.");
            return null;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
        _logger.LogInformation("Results refresh background service started (every {Minutes} min).", interval.TotalMinutes);
        return interval;
    }

    protected override async Task RunCycleAsync(IServiceProvider services, CancellationToken ct)
    {
        var updated = await services.GetRequiredService<IResultsUpdateService>().RefreshAllActiveRoundsAsync(ct);
        _logger.LogInformation("Background results refresh updated {Count} matches.", updated);
    }
}
