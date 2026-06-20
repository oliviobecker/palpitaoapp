using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Data;
using Sentry;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Periodically refreshes the results of in-play rounds from the configured provider
/// and re-stamps them for the temporary standings. Controlled by
/// <see cref="ResultsRefreshOptions.Enabled"/> (the option defaults to off; the shipped
/// appsettings enables it). Never closes a round. Errors are caught and logged so a
/// failed cycle can never take the host down.
/// </summary>
/// <remarks>
/// Multi-instance safe: when running on PostgreSQL, each cycle first tries a session-level
/// advisory lock, so only one instance performs the refresh at a time (the others skip the
/// cycle). This avoids duplicate external calls and write races when the API is scaled out.
/// </remarks>
public class ResultsRefreshBackgroundService : BackgroundService
{
    // Arbitrary, stable application-defined key for the refresh advisory lock.
    private const long ResultsRefreshLockKey = 4_125_990_001L;

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
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var refresher = scope.ServiceProvider.GetRequiredService<IResultsUpdateService>();
                await RunRefreshCycleAsync(db, refresher, stoppingToken);
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

    /// <summary>
    /// Runs one refresh cycle. On PostgreSQL it is guarded by a session advisory lock so
    /// only one instance refreshes per cycle; on other providers (e.g. tests) it just runs.
    /// </summary>
    private async Task RunRefreshCycleAsync(AppDbContext db, IResultsUpdateService refresher, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
        {
            var count = await refresher.RefreshAllActiveRoundsAsync(ct);
            _logger.LogInformation("Background results refresh updated {Count} matches.", count);
            return;
        }

        // Keep a single physical connection open so the session-level advisory lock is held
        // for the whole cycle, then released and the connection returned to the pool.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await TryAcquireLockAsync(db, ct))
            {
                _logger.LogDebug("Skipping results refresh cycle: another instance holds the lock.");
                return;
            }

            try
            {
                var updated = await refresher.RefreshAllActiveRoundsAsync(ct);
                _logger.LogInformation("Background results refresh updated {Count} matches.", updated);
            }
            finally
            {
                await ReleaseLockAsync(db, ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> TryAcquireLockAsync(AppDbContext db, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = ResultsRefreshLockKey;
        cmd.Parameters.Add(p);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task ReleaseLockAsync(AppDbContext db, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = ResultsRefreshLockKey;
        cmd.Parameters.Add(p);
        await cmd.ExecuteScalarAsync(ct);
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
