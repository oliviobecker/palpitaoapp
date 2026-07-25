using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Data;
using Sentry;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Deletes stored OCR upload bytes older than <see cref="OcrStorageOptions.RetentionDays"/>.
/// Only the images go — the batches, their candidates and the audit trail are untouched, so the
/// import history stays readable after the picture expires. Errors are caught and logged so a
/// failed cycle can never take the host down.
/// </summary>
/// <remarks>
/// Multi-instance safe: on PostgreSQL each cycle first tries a session-level advisory lock, so
/// only one instance sweeps at a time (the others skip the cycle), matching
/// <see cref="Results.ResultsRefreshBackgroundService"/>.
/// </remarks>
public class OcrImageRetentionBackgroundService : BackgroundService
{
    // Arbitrary, stable application-defined key for the retention advisory lock.
    private const long OcrImageRetentionLockKey = 4_125_990_002L;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OcrStorageOptions _options;
    private readonly ILogger<OcrImageRetentionBackgroundService> _logger;

    public OcrImageRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OcrStorageOptions> options,
        ILogger<OcrImageRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RetentionDays <= 0)
        {
            _logger.LogInformation("OCR image retention is disabled (RetentionDays <= 0).");
            return;
        }

        _logger.LogInformation(
            "OCR image retention started (keeping {Days} days).", _options.RetentionDays);

        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await RunSweepCycleAsync(db, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a bad cycle crash the host.
                _logger.LogError(ex, "OCR image retention sweep failed.");
                SentrySdk.CaptureException(ex);
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    /// <summary>
    /// Runs one sweep. On PostgreSQL it is guarded by a session advisory lock so only one instance
    /// sweeps per cycle; on other providers (e.g. tests) it just runs.
    /// </summary>
    private async Task RunSweepCycleAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
        {
            await DeleteExpiredAsync(db, ct);
            return;
        }

        // Keep a single physical connection open so the session-level advisory lock is held for
        // the whole cycle, then released and the connection returned to the pool.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await TryAcquireLockAsync(db, ct))
            {
                _logger.LogDebug("Skipping OCR image retention cycle: another instance holds the lock.");
                return;
            }

            try
            {
                await DeleteExpiredAsync(db, ct);
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

    private async Task DeleteExpiredAsync(AppDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var removed = await db.OcrImportImages
            .Where(i => i.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            _logger.LogInformation("OCR image retention removed {Count} expired images.", removed);
        }
    }

    private static async Task<bool> TryAcquireLockAsync(AppDbContext db, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = OcrImageRetentionLockKey;
        cmd.Parameters.Add(p);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task ReleaseLockAsync(AppDbContext db, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = OcrImageRetentionLockKey;
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
