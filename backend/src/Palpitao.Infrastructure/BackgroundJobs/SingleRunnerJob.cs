using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.Infrastructure.BackgroundJobs;

/// <summary>
/// A periodic job that runs on one instance at a time. Each cycle gets its own DI scope; on
/// PostgreSQL it first tries a session-level advisory lock on <see cref="LockKey"/>, so when the
/// API is scaled out only one instance does the work and the others skip the cycle (no duplicate
/// external calls, no write races). On other providers (e.g. tests) the cycle just runs. Errors
/// are caught and logged, so a failed cycle can never take the host down.
/// </summary>
public abstract class SingleRunnerJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected SingleRunnerJob(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>The job's advisory-lock key: application-defined, stable, unique per job.</summary>
    protected abstract long LockKey { get; }

    /// <summary>The job's name in log lines, e.g. "results refresh".</summary>
    protected abstract string Name { get; }

    /// <summary>How often to run, or null when the job is switched off (log why before returning).</summary>
    protected abstract TimeSpan? ResolveInterval();

    /// <summary>
    /// One cycle's work. Resolve what it needs from <paramref name="services"/>: the scope's
    /// <see cref="AppDbContext"/> is the one holding the lock's connection.
    /// </summary>
    protected abstract Task RunCycleAsync(IServiceProvider services, CancellationToken ct);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (ResolveInterval() is not TimeSpan interval)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunExclusivelyAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a bad cycle crash the host.
                _logger.LogError(ex, "Background {Job} cycle failed.", Name);
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunExclusivelyAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        if (!db.Database.IsNpgsql())
        {
            await RunCycleAsync(services, ct);
            return;
        }

        // Keep a single physical connection open so the session-level advisory lock is held for
        // the whole cycle, then released and the connection returned to the pool.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await AdvisoryLockAsync(db, "SELECT pg_try_advisory_lock(@key)", ct))
            {
                _logger.LogDebug("Skipping {Job} cycle: another instance holds the lock.", Name);
                return;
            }

            try
            {
                await RunCycleAsync(services, ct);
            }
            finally
            {
                await AdvisoryLockAsync(db, "SELECT pg_advisory_unlock(@key)", ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> AdvisoryLockAsync(AppDbContext db, string sql, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        var key = cmd.CreateParameter();
        key.ParameterName = "key";
        key.Value = LockKey;
        cmd.Parameters.Add(key);
        return await cmd.ExecuteScalarAsync(ct) is true;
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
