using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Ocr;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Ocr;

public class OcrServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class FakeOcrEngine : IOcrEngine
    {
        public string Result = string.Empty;
        public string ExtractText(byte[] image, string language) => Result;
    }

    private sealed class ThrowingOcrEngine : IOcrEngine
    {
        public string ExtractText(byte[] image, string language)
            => throw new InvalidOperationException("tessdata ausente");
    }

    /// <summary>A minimal byte array that passes the PNG header sniff.</summary>
    private static byte[] PngBytes(params byte[] payload)
        => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. payload];

    private static IOptions<OcrStorageOptions> Storage(OcrStorageOptions? options = null)
        => Options.Create(options ?? new OcrStorageOptions());

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        db.Seasons.Add(new Season
        {
            Id = SeasonId,
            Name = "England 2025/2026",
            StartDate = new DateOnly(2025, 8, 1),
            EndDate = new DateOnly(2026, 5, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db;
    }

    [Theory]
    [InlineData("palpites.pdf")]
    [InlineData("palpites.txt")]
    [InlineData("palpites")]
    public void ValidateFile_rejects_non_image(string fileName)
    {
        Assert.Throws<BusinessRuleException>(() => OcrService.ValidateFile(fileName, 1000));
    }

    [Fact]
    public void ValidateFile_accepts_image()
    {
        OcrService.ValidateFile("palpites.png", 1000); // should not throw
    }

    [Fact]
    public void ValidateFile_rejects_too_large()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => OcrService.ValidateFile("big.png", 11 * 1024 * 1024));
        Assert.Contains("10 MB", ex.Message);
    }

    [Fact]
    public async Task Process_creates_batch_with_text_and_candidates()
    {
        using var db = CreateContext();

        // Round with one match (Arsenal x Chelsea) + a participant named "João".
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService());
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = 1 }, Admin, Ct);
        await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
        {
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = DateTime.UtcNow.AddDays(2),
        }, Admin, Ct);

        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "João", Email = $"{userId}@x.com", PasswordHash = "x", Role = UserRole.Participant, IsActive = true, CreatedAt = DateTime.UtcNow });
        TestSeed.AddDefaultGroupMembership(db, userId);
        db.SaveChanges();

        var engine = new FakeOcrEngine { Result = "João\nArsenal 2x1 Chelsea" };
        var current = new FakeCurrentGroupService();
        var import = new PredictionImportService(db, new AuditService(db), current);
        var service = new OcrService(db, engine, import, new AuditService(db), current, Storage(), NullLogger<OcrService>.Instance);

        var batch = await service.ProcessAsync(round.Id, "palpites.png", PngBytes(1, 2, 3), "por", Admin, Ct);

        Assert.Equal(OcrBatchStatus.Processed, batch.Status);
        Assert.Equal("João\nArsenal 2x1 Chelsea", batch.ExtractedText);
        var candidate = Assert.Single(batch.Candidates);
        Assert.Equal(userId, candidate.UserId);
        Assert.Equal(2, candidate.PredictedHomeScore);
        Assert.False(candidate.NeedsReview);
    }

    // --- Review lifecycle (update / delete / cancel) -------------------------

    private static OcrService CreateService(
        AppDbContext db,
        ICurrentGroupService? current = null,
        IOcrEngine? engine = null,
        OcrStorageOptions? storage = null)
    {
        current ??= new FakeCurrentGroupService();
        return new OcrService(
            db, engine ?? new FakeOcrEngine(), new PredictionImportService(db, new AuditService(db), current),
            new AuditService(db), current, Storage(storage), NullLogger<OcrService>.Instance);
    }

    /// <summary>Seeds a round with one match and a batch holding one unresolved (noise) candidate.</summary>
    private static (Guid BatchId, Guid CandidateId, Guid MatchId, Guid UserId) SeedBatchWithCandidate(
        AppDbContext db, OcrBatchStatus status = OcrBatchStatus.Processed)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "João", Email = $"{userId}@x.com", PasswordHash = "x", Role = UserRole.Participant, IsActive = true, CreatedAt = DateTime.UtcNow });
        var round = new Round { Id = Guid.NewGuid(), SeasonId = SeasonId, Number = 1, Status = RoundStatus.Published, CreatedByUserId = Admin, CreatedAt = DateTime.UtcNow };
        db.Rounds.Add(round);
        var match = new RoundMatch { Id = Guid.NewGuid(), RoundId = round.Id, Competition = Competition.PremierLeague, Phase = MatchPhase.Regular, HomeTeamId = SeedIds.Arsenal, AwayTeamId = SeedIds.Chelsea, StartsAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.RoundMatches.Add(match);
        var batch = new OcrImportBatch { Id = Guid.NewGuid(), RoundId = round.Id, UploadedByUserId = Admin, OriginalFileName = "x.png", LanguageUsed = "por", Status = status, CreatedAt = DateTime.UtcNow };
        db.OcrImportBatches.Add(batch);
        var candidate = new OcrPredictionCandidate
        {
            Id = Guid.NewGuid(),
            OcrImportBatchId = batch.Id,
            RoundId = round.Id,
            ParticipantNameRaw = "ruído",
            MatchTextRaw = "linha ilegível",
            NeedsReview = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.OcrPredictionCandidates.Add(candidate);
        db.SaveChanges();
        return (batch.Id, candidate.Id, match.Id, userId);
    }

    [Fact]
    public async Task DeleteCandidate_removes_noise_and_marks_batch_reviewed()
    {
        using var db = CreateContext();
        var (batchId, candidateId, _, _) = SeedBatchWithCandidate(db);
        var service = CreateService(db);

        var dto = await service.DeleteCandidateAsync(batchId, candidateId, Admin, Ct);

        Assert.Empty(dto.Candidates);
        Assert.Equal(OcrBatchStatus.Reviewed, dto.Status);
        Assert.Empty(await db.OcrPredictionCandidates.ToListAsync());
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "OcrCandidateDeleted");
    }

    [Fact]
    public async Task DeleteCandidate_rejected_after_confirmation()
    {
        using var db = CreateContext();
        var (batchId, candidateId, _, _) = SeedBatchWithCandidate(db, OcrBatchStatus.Confirmed);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.DeleteCandidateAsync(batchId, candidateId, Admin, Ct));
        Assert.Equal("ocr.batchAlreadyConfirmed", ex.Key);
    }

    [Fact]
    public async Task Cancel_rejected_after_confirmation()
    {
        using var db = CreateContext();
        var (batchId, _, _, _) = SeedBatchWithCandidate(db, OcrBatchStatus.Confirmed);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelAsync(batchId, Admin, Ct));
        Assert.Equal("ocr.batchAlreadyConfirmed", ex.Key);
    }

    [Fact]
    public async Task UpdateCandidate_recalculates_confidence_and_marks_reviewed()
    {
        using var db = CreateContext();
        var (batchId, candidateId, matchId, userId) = SeedBatchWithCandidate(db);
        var service = CreateService(db);

        var dto = await service.UpdateCandidateAsync(batchId, candidateId, new UpdateOcrCandidateRequest
        {
            UserId = userId,
            RoundMatchId = matchId,
            PredictedHomeScore = 2,
            PredictedAwayScore = 1,
        }, Admin, Ct);

        var c = Assert.Single(dto.Candidates);
        Assert.False(c.NeedsReview);
        Assert.Equal(1.0, c.Confidence);
        Assert.Equal(OcrBatchStatus.Reviewed, dto.Status);
    }

    [Fact]
    public async Task UpdateCandidate_rejected_after_confirmation()
    {
        using var db = CreateContext();
        var (batchId, candidateId, matchId, userId) = SeedBatchWithCandidate(db, OcrBatchStatus.Confirmed);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateCandidateAsync(batchId, candidateId, new UpdateOcrCandidateRequest
            {
                UserId = userId,
                RoundMatchId = matchId,
                PredictedHomeScore = 1,
                PredictedAwayScore = 0,
            }, Admin, Ct));
        Assert.Equal("ocr.batchAlreadyConfirmed", ex.Key);
    }

    // --- Stored images -------------------------------------------------------

    /// <summary>
    /// Seeds a bare round with no matches or batches. Rounds are unique per (season, number), so
    /// each call takes its own number. Passing a group id creates that group too.
    /// </summary>
    private static Guid SeedRound(AppDbContext db, int number = 1, Guid? groupId = null)
    {
        if (groupId is not null)
        {
            db.Groups.Add(new Group
            {
                Id = groupId.Value,
                Name = $"Grupo {groupId}",
                Slug = $"grupo-{groupId}",
                CreatedByUserId = Admin,
                OwnerUserId = Admin,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        var round = new Round
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            Number = number,
            Status = RoundStatus.Published,
            CreatedByUserId = Admin,
            CreatedAt = DateTime.UtcNow,
        };
        if (groupId is not null)
        {
            round.GroupId = groupId.Value;
        }

        db.Rounds.Add(round);
        db.SaveChanges();
        return round.Id;
    }

    /// <summary>Seeds a batch with a stored image on an existing round.</summary>
    private static Guid SeedBatchWithImage(
        AppDbContext db, Guid roundId, byte[] bytes, DateTime? createdAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        var batch = new OcrImportBatch
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            UploadedByUserId = Admin,
            OriginalFileName = "x.png",
            LanguageUsed = "por",
            Status = OcrBatchStatus.Processed,
            CreatedAt = now,
        };
        db.OcrImportBatches.Add(batch);
        db.OcrImportImages.Add(new OcrImportImage
        {
            OcrImportBatchId = batch.Id,
            Content = bytes,
            ContentType = "image/png",
            FileExtension = ".png",
            ByteSize = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            CreatedAt = now,
        });
        db.SaveChanges();
        return batch.Id;
    }

    [Fact]
    public async Task Process_stores_the_uploaded_image()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var service = CreateService(db);
        var bytes = PngBytes(9, 8, 7, 6);

        var batch = await service.ProcessAsync(roundId, "palpites.png", bytes, "por", Admin, Ct);

        var stored = Assert.Single(await db.OcrImportImages.ToListAsync());
        Assert.Equal(batch.Id, stored.OcrImportBatchId);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(".png", stored.FileExtension);
        Assert.Equal(bytes.Length, stored.ByteSize);
        Assert.Equal(bytes, stored.Content);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), stored.Sha256);
        Assert.True(batch.HasImage);
    }

    [Fact]
    public async Task Process_stores_the_image_even_when_ocr_fails()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var service = CreateService(db, engine: new ThrowingOcrEngine());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ProcessAsync(roundId, "palpites.png", PngBytes(1), "por", Admin, Ct));

        Assert.Equal("ocr.processFailed", ex.Key);
        // The picture is the evidence for a failed read — it must survive the failure.
        Assert.Single(await db.OcrImportImages.ToListAsync());
        var batch = Assert.Single(await db.OcrImportBatches.ToListAsync());
        Assert.Equal(OcrBatchStatus.Failed, batch.Status);
    }

    [Fact]
    public async Task Process_skips_storing_when_disabled()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var service = CreateService(db, storage: new OcrStorageOptions { StoreImages = false });

        var batch = await service.ProcessAsync(roundId, "palpites.png", PngBytes(1), "por", Admin, Ct);

        Assert.Empty(await db.OcrImportImages.ToListAsync());
        Assert.False(batch.HasImage);
    }

    [Fact]
    public async Task Process_rejects_content_that_is_not_an_image()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var service = CreateService(db);
        var pdf = "%PDF-1.4\n"u8.ToArray();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ProcessAsync(roundId, "palpites.png", pdf, "por", Admin, Ct));

        Assert.Equal("ocr.contentMismatch", ex.Key);
        // Rejected before any DB work: no junk Failed batch left behind.
        Assert.Empty(await db.OcrImportBatches.ToListAsync());
        Assert.Empty(await db.OcrImportImages.ToListAsync());
    }

    [Fact]
    public async Task Process_rejects_extension_that_disagrees_with_content()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var service = CreateService(db);
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ProcessAsync(roundId, "palpites.png", jpeg, "por", Admin, Ct));

        Assert.Equal("ocr.contentMismatch", ex.Key);
        Assert.Empty(await db.OcrImportBatches.ToListAsync());
    }

    [Fact]
    public async Task GetImage_returns_the_stored_bytes()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var bytes = PngBytes(4, 5, 6);
        var batchId = SeedBatchWithImage(db, roundId, bytes);
        var service = CreateService(db);

        var image = await service.GetImageAsync(batchId, Ct);

        Assert.Equal(bytes, image.Content);
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), image.Sha256);
    }

    [Fact]
    public async Task GetImage_is_scoped_to_the_group()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db, groupId: Guid.NewGuid());
        var batchId = SeedBatchWithImage(db, roundId, PngBytes(1));
        // Caller is an admin of the default group, the batch belongs to another one.
        var service = CreateService(db, new FakeCurrentGroupService());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.GetImageAsync(batchId, Ct));

        // Same key as a non-existent batch: no cross-tenant existence leak.
        Assert.Equal("notFound.ocrBatch", ex.Key);
    }

    [Fact]
    public async Task GetImage_returns_not_found_for_a_batch_without_an_image()
    {
        using var db = CreateContext();
        var (batchId, _, _, _) = SeedBatchWithCandidate(db);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.GetImageAsync(batchId, Ct));

        Assert.Equal("notFound.ocrImage", ex.Key);
    }

    [Fact]
    public async Task GetBatch_does_not_load_the_image_bytes()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var batchId = SeedBatchWithImage(db, roundId, PngBytes(1, 2, 3));
        db.ChangeTracker.Clear();
        var service = CreateService(db);

        var dto = await service.GetBatchAsync(batchId, Ct);

        Assert.True(dto.HasImage);
        // Guards against a future Include(b => b.Image) dragging the blob onto the review path.
        Assert.Empty(db.ChangeTracker.Entries<OcrImportImage>());
    }

    [Fact]
    public async Task ListBatches_summarises_without_the_bytes()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var otherRoundId = SeedRound(db, number: 2);
        var older = DateTime.UtcNow.AddHours(-2);
        var oldBatchId = SeedBatchWithImage(db, roundId, PngBytes(1), older);
        var newBatchId = SeedBatchWithImage(db, roundId, PngBytes(1, 2, 3, 4), DateTime.UtcNow);
        SeedBatchWithImage(db, otherRoundId, PngBytes(9));
        db.ChangeTracker.Clear();
        var service = CreateService(db);

        var list = await service.ListBatchesAsync(roundId, Ct);

        Assert.Equal([newBatchId, oldBatchId], list.Select(b => b.Id));
        Assert.All(list, b => Assert.True(b.HasImage));
        Assert.Equal(12, list[0].ImageByteSize);
        Assert.Equal("image/png", list[0].ImageContentType);
        Assert.Empty(db.ChangeTracker.Entries<OcrImportImage>());
    }

    [Fact]
    public async Task ListBatches_reports_a_batch_whose_image_was_pruned()
    {
        using var db = CreateContext();
        var (batchId, _, _, _) = SeedBatchWithCandidate(db);
        var roundId = (await db.OcrImportBatches.FirstAsync(b => b.Id == batchId)).RoundId;
        var service = CreateService(db);

        var summary = Assert.Single(await service.ListBatchesAsync(roundId, Ct));

        Assert.False(summary.HasImage);
        Assert.Null(summary.ImageByteSize);
        Assert.Equal(1, summary.CandidateCount);
    }

    [Fact]
    public async Task ListBatches_is_scoped_to_the_group()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db, groupId: Guid.NewGuid());
        SeedBatchWithImage(db, roundId, PngBytes(1));
        var service = CreateService(db, new FakeCurrentGroupService());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.ListBatchesAsync(roundId, Ct));

        Assert.Equal("notFound.round", ex.Key);
    }

    [Fact]
    public async Task Process_prunes_the_oldest_images_beyond_the_cap()
    {
        using var db = CreateContext();
        var roundId = SeedRound(db);
        var oldest = SeedBatchWithImage(db, roundId, PngBytes(1), DateTime.UtcNow.AddHours(-3));
        var middle = SeedBatchWithImage(db, roundId, PngBytes(2), DateTime.UtcNow.AddHours(-2));
        var service = CreateService(db, storage: new OcrStorageOptions { MaxImagesPerRound = 2 });

        var batch = await service.ProcessAsync(roundId, "palpites.png", PngBytes(3), "por", Admin, Ct);

        var remaining = await db.OcrImportImages.Select(i => i.OcrImportBatchId).ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.Contains(batch.Id, remaining);
        Assert.Contains(middle, remaining);
        Assert.DoesNotContain(oldest, remaining);
        // Only the bytes go: the batch row and its history survive.
        Assert.Equal(3, await db.OcrImportBatches.CountAsync());
    }
}
