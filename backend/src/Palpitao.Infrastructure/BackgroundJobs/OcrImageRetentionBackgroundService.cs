using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Palpitao.Application.Ocr;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.Infrastructure.BackgroundJobs;

/// <summary>
/// Deletes stored OCR upload bytes older than <see cref="OcrStorageOptions.RetentionDays"/>.
/// Only the images go — the batches, their candidates and the audit trail are untouched, so the
/// import history stays readable after the picture expires. One instance at a time (see
/// <see cref="SingleRunnerJob"/>).
/// </summary>
public sealed class OcrImageRetentionBackgroundService : SingleRunnerJob
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromDays(1);

    private readonly OcrStorageOptions _options;
    private readonly ILogger<OcrImageRetentionBackgroundService> _logger;

    public OcrImageRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OcrStorageOptions> options,
        ILogger<OcrImageRetentionBackgroundService> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Arbitrary, stable application-defined key for the retention advisory lock.
    protected override long LockKey => 4_125_990_002L;

    protected override string Name => "OCR image retention";

    protected override TimeSpan? ResolveInterval()
    {
        if (_options.RetentionDays <= 0)
        {
            _logger.LogInformation("OCR image retention is disabled (RetentionDays <= 0).");
            return null;
        }

        _logger.LogInformation("OCR image retention started (keeping {Days} days).", _options.RetentionDays);
        return SweepInterval;
    }

    protected override async Task RunCycleAsync(IServiceProvider services, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var removed = await services.GetRequiredService<AppDbContext>().OcrImportImages
            .Where(i => i.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            _logger.LogInformation("OCR image retention removed {Count} expired images.", removed);
        }
    }
}
