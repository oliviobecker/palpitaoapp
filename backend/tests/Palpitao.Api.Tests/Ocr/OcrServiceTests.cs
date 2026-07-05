using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
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
        var service = new OcrService(db, engine, import, new AuditService(db), current, NullLogger<OcrService>.Instance);

        var batch = await service.ProcessAsync(round.Id, "palpites.png", new byte[] { 1, 2, 3 }, "por", Admin, Ct);

        Assert.Equal(OcrBatchStatus.Processed, batch.Status);
        Assert.Equal("João\nArsenal 2x1 Chelsea", batch.ExtractedText);
        var candidate = Assert.Single(batch.Candidates);
        Assert.Equal(userId, candidate.UserId);
        Assert.Equal(2, candidate.PredictedHomeScore);
        Assert.False(candidate.NeedsReview);
    }

    // --- Review lifecycle (update / delete / cancel) -------------------------

    private static OcrService CreateService(AppDbContext db)
    {
        var current = new FakeCurrentGroupService();
        return new OcrService(
            db, new FakeOcrEngine(), new PredictionImportService(db, new AuditService(db), current),
            new AuditService(db), current, NullLogger<OcrService>.Instance);
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
}
