using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
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
}
