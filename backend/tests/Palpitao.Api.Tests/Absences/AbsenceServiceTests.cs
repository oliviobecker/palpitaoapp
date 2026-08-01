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

    private static AbsenceService Service(AppDbContext db) => new(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));

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
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));
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

    /// <summary>Persists a custom rule set for the season (mirrors what the admin screen saves).</summary>
    private static void ConfigureRules(
        AppDbContext db, int penaltyPoints = 20, int eliminationCount = 5, int absenceFromRound = 1)
    {
        db.SeasonScoringConfigs.Add(new SeasonScoringConfig
        {
            Id = Guid.NewGuid(),
            GroupId = SeedIds.DefaultGroup,
            SeasonId = SeasonId,
            ColumnOnlyPoints = 1,
            TraditionalPoints = 3,
            MediumPoints = 5,
            UncommonPoints = 7,
            ExtraUncommonPoints = 10,
            AbsencePenaltyPoints = penaltyPoints,
            AbsenceEliminationCount = eliminationCount,
            AbsenceFromRound = absenceFromRound,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Custom_penalty_and_elimination_ordinal_are_honoured()
    {
        using var db = CreateContext();
        ConfigureRules(db, penaltyPoints: 10, eliminationCount: 3);
        var service = Service(db);
        var user = CreateParticipant(db);

        var expected = new (int Number, int Penalty, bool Eliminated)[]
        {
            (1, 0, false),
            (2, 0, false),
            (3, 0, true), // elimination is checked first, so the 3rd both eliminates and costs nothing
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
    public async Task Custom_penalty_applies_before_the_elimination_ordinal()
    {
        using var db = CreateContext();
        ConfigureRules(db, penaltyPoints: 10, eliminationCount: 5);
        var service = Service(db);
        var user = CreateParticipant(db);

        AbsenceOutcome? third = null;
        foreach (var number in new[] { 1, 2, 3 })
        {
            var round = await PublishedRound(db, number);
            var outcomes = await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
            third = Assert.Single(outcomes, o => o.UserId == user);
        }

        Assert.Equal(10, third!.PenaltyPoints);
        Assert.False(third.Eliminated);
    }

    [Fact]
    public async Task Rounds_before_the_threshold_zero_the_round_without_counting()
    {
        using var db = CreateContext();
        ConfigureRules(db, absenceFromRound: 3);
        var service = Service(db);
        var user = CreateParticipant(db);

        var round1 = await PublishedRound(db, 1);
        var outcome1 = Assert.Single(
            await service.ProcessRoundAbsencesAsync(round1.Id, SeedIds.AdminUser, Ct), o => o.UserId == user);

        // Not on the ladder: no ordinal, no penalty, no Absence row...
        Assert.Equal(0, outcome1.AbsenceNumber);
        Assert.Equal(0, outcome1.PenaltyPoints);
        Assert.False(outcome1.Eliminated);
        Assert.Empty(db.Absences.Where(a => a.RoundId == round1.Id));

        // ...but the participant still has a zeroed result row for the round.
        var result = db.RoundParticipantResults.Single(r => r.RoundId == round1.Id && r.UserId == user);
        Assert.True(result.WasAbsent);
        Assert.Equal(0, result.FinalPoints);

        // The first counted absence is the one in round 3, numbered 1.
        var round3 = await PublishedRound(db, 3);
        var outcome3 = Assert.Single(
            await service.ProcessRoundAbsencesAsync(round3.Id, SeedIds.AdminUser, Ct), o => o.UserId == user);
        Assert.Equal(1, outcome3.AbsenceNumber);
    }

    [Fact]
    public async Task Raising_the_threshold_and_reprocessing_clears_stale_absences()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var round = await PublishedRound(db, 1);
        await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        Assert.Single(db.Absences.Where(a => a.RoundId == round.Id));

        // The admin now starts punishing only from round 5 on, then re-scores.
        ConfigureRules(db, absenceFromRound: 5);
        await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);

        Assert.Empty(db.Absences.Where(a => a.RoundId == round.Id));
        Assert.True(db.RoundParticipantResults.Single(r => r.RoundId == round.Id && r.UserId == user).WasAbsent);
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
