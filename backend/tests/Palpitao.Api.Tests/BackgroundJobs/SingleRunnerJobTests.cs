using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Palpitao.Application.Ocr;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.BackgroundJobs;
using Palpitao.Infrastructure.Persistence;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.Api.Tests.BackgroundJobs;

/// <summary>
/// The job loop on SQLite, where the advisory lock does not apply and every cycle just runs. The
/// first cycle runs at start-up, so each test starts the job, waits for a signal and stops it.
/// </summary>
public sealed class SingleRunnerJobTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;

    public SingleRunnerJobTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
            .BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private IServiceScopeFactory ScopeFactory => _services.GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public async Task The_first_cycle_runs_at_start()
    {
        var job = new TestJob(ScopeFactory, new RecordingLogger(), TimeSpan.FromHours(1));

        await job.StartAsync(CancellationToken.None);
        var context = await job.CycleRan.Task.WaitAsync(Wait);
        await job.StopAsync(CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(1, job.Cycles);
    }

    [Fact]
    public async Task A_switched_off_job_never_runs_a_cycle()
    {
        var job = new TestJob(ScopeFactory, new RecordingLogger(), interval: null);

        await job.StartAsync(CancellationToken.None);
        await job.ExecuteTask!.WaitAsync(Wait);
        await job.StopAsync(CancellationToken.None);

        Assert.Equal(0, job.Cycles);
    }

    [Fact]
    public async Task A_failing_cycle_is_logged_and_does_not_stop_the_job()
    {
        var logger = new RecordingLogger();
        var job = new TestJob(ScopeFactory, logger, TimeSpan.FromHours(1), fail: true);

        await job.StartAsync(CancellationToken.None);
        var error = await logger.FirstError.Task.WaitAsync(Wait);

        Assert.Equal("Background test job cycle failed.", error);
        Assert.False(job.ExecuteTask!.IsCompleted); // still waiting for the next tick
        await job.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_retention_sweep_deletes_only_expired_images()
    {
        var (expired, recent) = SeedImages(DateTime.UtcNow.AddDays(-31), DateTime.UtcNow.AddDays(-1));
        var logger = new RecordingLogger<OcrImageRetentionBackgroundService>();
        var job = new OcrImageRetentionBackgroundService(
            ScopeFactory, Options.Create(new OcrStorageOptions { RetentionDays = 30 }), logger);

        var removed = logger.Logged("OCR image retention removed 1 expired images.");

        await job.StartAsync(CancellationToken.None);
        await removed.Task.WaitAsync(Wait);
        await job.StopAsync(CancellationToken.None);

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(recent, Assert.Single(await db.OcrImportImages.Select(i => i.OcrImportBatchId).ToListAsync()));
        // Only the bytes go: both batches (and so their candidates and history) stay.
        Assert.Equal(2, await db.OcrImportBatches.CountAsync(b => b.Id == expired || b.Id == recent));
    }

    private (Guid Expired, Guid Recent) SeedImages(DateTime expiredAt, DateTime recentAt)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var season = new Season
        {
            Id = Guid.NewGuid(),
            Name = "England 2025/2026",
            StartDate = new DateOnly(2025, 8, 1),
            EndDate = new DateOnly(2026, 5, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        var round = new Round
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            Number = 1,
            Status = RoundStatus.Published,
            CreatedByUserId = SeedIds.AdminUser,
            CreatedAt = DateTime.UtcNow,
        };
        db.Seasons.Add(season);
        db.Rounds.Add(round);

        Guid AddBatch(DateTime at)
        {
            var batch = new OcrImportBatch
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                UploadedByUserId = SeedIds.AdminUser,
                OriginalFileName = "x.png",
                LanguageUsed = "por",
                Status = OcrBatchStatus.Processed,
                CreatedAt = at,
            };
            db.OcrImportBatches.Add(batch);
            db.OcrImportImages.Add(new OcrImportImage
            {
                OcrImportBatchId = batch.Id,
                Content = [1, 2, 3],
                ContentType = "image/png",
                FileExtension = ".png",
                ByteSize = 3,
                Sha256 = new string('0', 64),
                CreatedAt = at,
            });
            return batch.Id;
        }

        var ids = (AddBatch(expiredAt), AddBatch(recentAt));
        db.SaveChanges();
        return ids;
    }

    private sealed class TestJob(IServiceScopeFactory scopeFactory, ILogger logger, TimeSpan? interval, bool fail = false)
        : SingleRunnerJob(scopeFactory, logger)
    {
        public TaskCompletionSource<AppDbContext> CycleRan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Cycles { get; private set; }

        protected override long LockKey => 1;

        protected override string Name => "test job";

        protected override TimeSpan? ResolveInterval() => interval;

        protected override Task RunCycleAsync(IServiceProvider services, CancellationToken ct)
        {
            Cycles++;
            if (fail)
            {
                throw new InvalidOperationException("boom");
            }

            CycleRan.TrySetResult(services.GetRequiredService<AppDbContext>());
            return Task.CompletedTask;
        }
    }

    private class RecordingLogger : ILogger
    {
        private readonly List<(string Message, TaskCompletionSource<string> Signal)> _waits = [];

        public TaskCompletionSource<string> FirstError { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes when a log line renders to exactly <paramref name="message"/>.</summary>
        public TaskCompletionSource<string> Logged(string message)
        {
            var signal = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_waits)
            {
                _waits.Add((message, signal));
            }

            return signal;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel == LogLevel.Error)
            {
                FirstError.TrySetResult(message);
            }

            lock (_waits)
            {
                foreach (var (expected, signal) in _waits.Where(w => w.Message == message))
                {
                    signal.TrySetResult(message);
                }
            }
        }
    }

    private sealed class RecordingLogger<T> : RecordingLogger, ILogger<T>;
}
