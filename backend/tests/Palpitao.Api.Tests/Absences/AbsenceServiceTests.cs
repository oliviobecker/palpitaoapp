using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Absences;

public class AbsenceServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
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

    private static AbsenceService Service(AppDbContext db) => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static Guid CreateParticipant(AppDbContext db)
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
        TestSeed.AddDefaultGroupMembership(db, id);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> PublishedRound(AppDbContext db, int number, int matchCount = 1)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService());
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number }, SeedIds.AdminUser, Ct);
        for (var i = 0; i < matchCount; i++)
        {
            await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
            {
                Competition = Competition.PremierLeague,
                Phase = MatchPhase.Regular,
                HomeTeamId = Pairs[i].Home,
                AwayTeamId = Pairs[i].Away,
                StartsAt = Future.AddHours(i),
            }, SeedIds.AdminUser, Ct);
        }
        return await rounds.PublishAsync(round.Id, SeedIds.AdminUser, Ct);
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Absence_penalties_progress_and_eliminate_on_fifth()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var expected = new (int Number, int Penalty, bool Eliminated)[]
        {
            (1, 0, false),
            (2, 0, false),
            (3, 20, false),
            (4, 20, false),
            (5, 0, true),
        };

        foreach (var (number, penalty, eliminated) in expected)
        {
            var round = await PublishedRound(db, number);
            var outcomes = await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);

            var outcome = Assert.Single(outcomes, o => o.UserId == user);
            Assert.Equal(number, outcome.AbsenceNumber);
            Assert.Equal(penalty, outcome.PenaltyPoints);
            Assert.Equal(eliminated, outcome.Eliminated);
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));
    }

    [Fact]
    public async Task Participant_with_incomplete_predictions_is_absent()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(db, 1, matchCount: 2);

        // Insert a single prediction directly (1 of 2 matches) => incomplete.
        var firstMatchId = round.Matches[0].Id;
        db.Predictions.Add(new Prediction
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            RoundMatchId = firstMatchId,
            UserId = user,
            PredictedHomeScore = 1,
            PredictedAwayScore = 0,
            SubmittedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        Assert.True(await service.IsAbsentAsync(round.Id, user, Ct));
    }

    [Fact]
    public async Task Participant_with_all_predictions_is_not_absent()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(db, 1, matchCount: 2);

        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());
        await predictions.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest
        {
            Predictions = round.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = 1,
                PredictedAwayScore = 1,
            }).ToList(),
        }, isEdit: false, Ct);

        Assert.False(await service.IsAbsentAsync(round.Id, user, Ct));
    }

    [Fact]
    public async Task Eliminated_participant_cannot_predict_future_round()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 5; i++)
        {
            var round = await PublishedRound(db, i);
            await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        }

        var future = await PublishedRound(db, 6);
        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            predictions.SavePredictionsAsync(future.Id, user, new SavePredictionsRequest
            {
                Predictions = future.Matches.Select(m => new PredictionItemRequest
                {
                    RoundMatchId = m.Id,
                    PredictedHomeScore = 1,
                    PredictedAwayScore = 0,
                }).ToList(),
            }, false, Ct));

        Assert.Contains("eliminado", ex.Message);
    }

    [Fact]
    public async Task Admin_can_reactivate_eliminated_participant()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 5; i++)
        {
            var round = await PublishedRound(db, i);
            await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));

        await service.ReactivateAsync(user, "Reativação após acordo na liga.", SeedIds.AdminUser, Ct);

        Assert.False(TestSeed.IsEliminatedInDefaultGroup(db, user));
        Assert.True(TestSeed.IsActiveInDefaultGroup(db, user));
    }

    [Fact]
    public async Task Override_can_excuse_a_participant_from_absence()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(db, 1);

        // No predictions => would be absent, but admin excuses them.
        await service.ApplyOverrideAsync(round.Id, new AbsenceOverrideRequest
        {
            UserId = user,
            IsAbsent = false,
            Justification = "Problema técnico comprovado.",
        }, SeedIds.AdminUser, Ct);

        Assert.False(await service.IsAbsentAsync(round.Id, user, Ct));
    }
}
