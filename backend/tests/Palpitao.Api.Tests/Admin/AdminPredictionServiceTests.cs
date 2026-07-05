using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.AdminPredictions;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Admin;

public class AdminPredictionServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    private static readonly (Guid Home, Guid Away)[] Pairs =
    {
        (SeedIds.Arsenal, SeedIds.Chelsea),
        (SeedIds.Liverpool, SeedIds.Newcastle),
    };

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

    private static AdminPredictionService Service(AppDbContext db) => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static Guid CreateParticipant(AppDbContext db, bool eliminated = false)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = $"P{id.ToString()[..4]}",
            Email = $"user-{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        TestSeed.AddDefaultGroupMembership(db, id, isEliminated: eliminated);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> PublishedRound(AppDbContext db, int matchCount = 2)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService());
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = 1 }, Admin, Ct);
        for (var i = 0; i < matchCount; i++)
        {
            await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
            {
                Competition = Competition.PremierLeague,
                Phase = MatchPhase.Regular,
                HomeTeamId = Pairs[i].Home,
                AwayTeamId = Pairs[i].Away,
                StartsAt = Future.AddHours(i),
            }, Admin, Ct);
        }
        return await rounds.PublishAsync(round.Id, Admin, Ct);
    }

    private static ManualPredictionRequest FullRequest(Guid userId, RoundDto round, bool overwrite = false, string? justification = null)
        => new()
        {
            UserId = userId,
            OverwriteExisting = overwrite,
            Justification = justification,
            Predictions = round.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = 1,
                PredictedAwayScore = 0,
            }).ToList(),
        };

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_register_predictions_with_source_adminmanual()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);

        await service.SaveManualAsync(round.Id, FullRequest(user, round), Admin, Ct);

        var predictions = await db.Predictions.Where(p => p.UserId == user).ToListAsync();
        Assert.Equal(round.Matches.Count, predictions.Count);
        Assert.All(predictions, p => Assert.Equal(PredictionSource.AdminManual, p.Source));
        Assert.All(predictions, p => Assert.Equal(Admin, p.CreatedByUserId));
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "AdminPredictionCreated");
    }

    [Fact]
    public async Task Admin_cannot_register_incomplete()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);
        var request = FullRequest(user, round);
        request.Predictions.RemoveAt(0);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveManualAsync(round.Id, request, Admin, Ct));
    }

    [Fact]
    public async Task Admin_cannot_register_negative_score()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);
        var request = FullRequest(user, round);
        request.Predictions[0].PredictedHomeScore = -1;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveManualAsync(round.Id, request, Admin, Ct));
        Assert.Contains("negativo", ex.Message);
    }

    [Fact]
    public async Task Admin_cannot_register_for_eliminated_without_override()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db, eliminated: true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SaveManualAsync(round.Id, FullRequest(user, round), Admin, Ct));
        Assert.Contains("eliminado", ex.Message);
    }

    [Fact]
    public async Task Admin_can_register_for_eliminated_with_justified_override()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db, eliminated: true);

        var request = FullRequest(user, round, justification: "Acordo na liga.");
        request.AllowAfterDeadline = true;

        await service.SaveManualAsync(round.Id, request, Admin, Ct);
        Assert.Equal(round.Matches.Count, await db.Predictions.CountAsync(p => p.UserId == user));
    }

    [Fact]
    public async Task Admin_cannot_overwrite_without_confirmation()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);

        await service.SaveManualAsync(round.Id, FullRequest(user, round), Admin, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SaveManualAsync(round.Id, FullRequest(user, round), Admin, Ct));
        Assert.Contains("já possui palpites", ex.Message);
    }

    [Fact]
    public async Task Admin_can_overwrite_with_confirmation()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);

        await service.SaveManualAsync(round.Id, FullRequest(user, round), Admin, Ct);
        await service.SaveManualAsync(round.Id, FullRequest(user, round, overwrite: true, justification: "Correção."), Admin, Ct);

        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "AdminPredictionOverwritten");
        Assert.Equal(round.Matches.Count, await db.Predictions.CountAsync(p => p.UserId == user));
    }

    [Fact]
    public async Task GetParticipantPredictions_returns_empty_when_none()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);

        var result = await service.GetParticipantPredictionsAsync(round.Id, user, Ct);

        Assert.False(result.HasPredictions);
        Assert.Empty(result.Predictions);
        Assert.Equal(user, result.UserId);
        Assert.Equal(round.Id, result.RoundId);
    }

    [Fact]
    public async Task GetParticipantPredictions_returns_existing_with_source()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);
        var user = CreateParticipant(db);
        await service.SaveManualAsync(round.Id, FullRequest(user, round), Admin, Ct);

        var result = await service.GetParticipantPredictionsAsync(round.Id, user, Ct);

        Assert.True(result.HasPredictions);
        Assert.Equal(round.Matches.Count, result.Predictions.Count);
        Assert.All(result.Predictions, p => Assert.Equal(PredictionSource.AdminManual, p.Source));
        Assert.All(result.Predictions, p => Assert.Equal(1, p.PredictedHomeScore));
    }

    [Fact]
    public async Task GetParticipantPredictions_throws_for_unknown_round()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetParticipantPredictionsAsync(Guid.NewGuid(), user, Ct));
    }

    [Fact]
    public async Task GetParticipantPredictions_throws_for_unknown_participant()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetParticipantPredictionsAsync(round.Id, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task GetCoverage_splits_complete_and_missing_participants()
    {
        using var db = CreateContext();
        var service = Service(db);
        var round = await PublishedRound(db); // 2 matches
        var complete = CreateParticipant(db);
        var missing = CreateParticipant(db);
        var eliminated = CreateParticipant(db, eliminated: true); // must not count
        await service.SaveManualAsync(round.Id, FullRequest(complete, round), Admin, Ct);
        // "missing" predicted only the first match.
        db.Predictions.Add(new Prediction
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            RoundMatchId = round.Matches[0].Id,
            UserId = missing,
            PredictedHomeScore = 1,
            PredictedAwayScore = 1,
            SubmittedAt = DateTime.UtcNow,
            Source = PredictionSource.Participant,
        });
        db.SaveChanges();

        var coverage = await service.GetCoverageAsync(round.Id, Ct);

        Assert.Equal(2, coverage.MatchCount);
        Assert.Equal(2, coverage.TotalParticipants); // eliminated excluded
        Assert.Equal(1, coverage.CompleteParticipants);
        var pending = Assert.Single(coverage.Missing);
        Assert.Equal(missing, pending.UserId);
        Assert.Equal(1, pending.PredictedCount);
        Assert.DoesNotContain(coverage.Missing, p => p.UserId == eliminated);
    }

    [Fact]
    public async Task GetCoverage_throws_for_unknown_round()
    {
        using var db = CreateContext();
        var service = Service(db);
        CreateParticipant(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCoverageAsync(Guid.NewGuid(), Ct));
    }
}
