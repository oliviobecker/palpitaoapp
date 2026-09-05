using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Admin;

/// <summary>
/// An admin excusing a participant's absences in closed rounds (typically rounds played before
/// the participant actually joined) writes "present" overrides and replays the season in one
/// transaction, so the absence ladder, penalties and eliminations are re-derived for everyone.
/// </summary>
public class ParticipantAbsenceReviewTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

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
        TestSeed.AddNeutralTeams(db);
        db.SaveChanges();
        return db;
    }

    private static RoundService Rounds(AppDbContext db)
        => new(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));

    private static Guid CreateParticipant(AppDbContext db, string name)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = name,
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

    /// <summary>A one-match neutral round (multiplier 1), published so predictions are accepted.</summary>
    private static async Task<RoundDto> PublishedRound(AppDbContext db, int number)
    {
        var rounds = Rounds(db);
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number }, Admin, Ct);
        await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
        {
            Competition = Competition.Championship,
            Phase = MatchPhase.Regular,
            HomeTeamId = TestSeed.NeutralPairs[0].Home,
            AwayTeamId = TestSeed.NeutralPairs[0].Away,
            StartsAt = Future,
        }, Admin, Ct);
        return await rounds.PublishAsync(round.Id, Admin, Ct);
    }

    /// <summary>Exact 2x1 for every match: a Traditional hit worth 3 points at multiplier 1.</summary>
    private static async Task Predict(AppDbContext db, RoundDto round, Guid user)
    {
        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());
        await predictions.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest
        {
            Predictions = round.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = 2,
                PredictedAwayScore = 1,
            }).ToList(),
        }, false, Ct);
    }

    /// <summary>
    /// Publishes a round, records the given participants' predictions, locks it, enters the 2x1
    /// result and scores it. Everyone else on the roster comes out absent.
    /// </summary>
    private static async Task<RoundDto> ScoredRound(AppDbContext db, int number, params Guid[] predicting)
    {
        var round = await PublishedRound(db, number);
        foreach (var user in predicting)
        {
            await Predict(db, round, user);
        }

        await Rounds(db).LockAsync(round.Id, Admin, Ct);
        var scoring = TestServices.RoundScoring(db);
        await scoring.SetMatchResultAsync(round.Matches[0].Id,
            new MatchResultRequest { HomeScore = 2, AwayScore = 1 }, Admin, Ct);
        await scoring.ScoreRoundAsync(round.Id, Admin, Ct);
        return round;
    }

    private static AbsenceReviewRequest Review(string justification, params (Guid RoundId, bool IsAbsent)[] decisions)
        => new()
        {
            Justification = justification,
            Rounds = decisions.Select(d => new AbsenceReviewDecision { RoundId = d.RoundId, IsAbsent = d.IsAbsent }).ToList(),
        };

    private static Standing StoredStanding(AppDbContext db, Guid userId)
        => db.Standings.AsNoTracking().Single(s => s.SeasonId == SeasonId && s.UserId == userId);

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Reviewing_absences_excuses_the_rounds_and_reshuffles_the_ladder_for_everyone()
    {
        using var db = CreateContext();
        var joao = CreateParticipant(db, "João Paulo");
        var bruno = CreateParticipant(db, "Bruno");

        // Five scored rounds João never predicted: 5th absence -> eliminated, 3rd/4th -> -20 each.
        var rounds = new List<RoundDto>();
        for (var number = 1; number <= 5; number++)
        {
            rounds.Add(await ScoredRound(db, number, bruno));
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, joao));
        Assert.Equal(40, StoredStanding(db, joao).PenaltyPoints);

        // He only joined for round 4: rounds 1-3 must not count as absences.
        var result = await TestServices.RoundScoring(db).ReviewParticipantAbsencesAsync(joao, Review(
            "Ainda não participava do bolão.",
            (rounds[0].Id, false), (rounds[1].Id, false), (rounds[2].Id, false),
            (rounds[3].Id, true), (rounds[4].Id, true)), Admin, Ct);

        Assert.Equal(3, result.ChangedRounds);
        Assert.True(result.Recalculated);
        db.ChangeTracker.Clear();

        // Excused rounds: zeroed but present, so nothing on the ladder.
        foreach (var excused in rounds.Take(3))
        {
            var stored = db.RoundParticipantResults.Single(r => r.RoundId == excused.Id && r.UserId == joao);
            Assert.False(stored.WasAbsent);
            Assert.Equal(0, stored.FinalPoints);
            Assert.Equal(0, stored.PenaltyPoints);
            Assert.Empty(db.Absences.Where(a => a.RoundId == excused.Id && a.UserId == joao));
        }

        // The remaining absences are renumbered from 1, so no penalty and no elimination.
        Assert.Equal(1, db.Absences.Single(a => a.RoundId == rounds[3].Id && a.UserId == joao).AbsenceNumber);
        Assert.Equal(2, db.Absences.Single(a => a.RoundId == rounds[4].Id && a.UserId == joao).AbsenceNumber);
        Assert.False(TestSeed.IsEliminatedInDefaultGroup(db, joao));

        var joaoStanding = StoredStanding(db, joao);
        Assert.Equal(2, joaoStanding.AbsenceCount);
        Assert.Equal(0, joaoStanding.PenaltyPoints);
        Assert.Equal(3, joaoStanding.PlayedRounds);
        Assert.Equal(0, joaoStanding.TotalPoints);

        // Bruno's five exact hits are untouched by the replay.
        Assert.Equal(15, StoredStanding(db, bruno).TotalPoints);

        var overrides = db.AbsenceOverrides.Where(o => o.UserId == joao).ToList();
        Assert.Equal(3, overrides.Count);
        Assert.All(overrides, o => Assert.False(o.IsAbsent));
        Assert.All(overrides, o => Assert.Equal("Ainda não participava do bolão.", o.Justification));
        Assert.Contains(db.AuditLogs, a => a.Action == "ParticipantAbsencesReviewed");
        Assert.Contains(db.AuditLogs, a => a.Action == "SeasonRecalculated");
    }

    [Fact]
    public async Task Reviewing_a_locked_round_only_stores_the_override_without_recalculating()
    {
        using var db = CreateContext();
        var joao = CreateParticipant(db, "João Paulo");
        var bruno = CreateParticipant(db, "Bruno");
        var scored = await ScoredRound(db, 1, bruno);
        var locked = await PublishedRound(db, 2);
        await Rounds(db).LockAsync(locked.Id, Admin, Ct);

        var result = await TestServices.RoundScoring(db).ReviewParticipantAbsencesAsync(
            joao, Review("Ainda não participava do bolão.", (locked.Id, false)), Admin, Ct);

        Assert.Equal(1, result.ChangedRounds);
        Assert.False(result.Recalculated);
        db.ChangeTracker.Clear();

        var stored = db.AbsenceOverrides.Single(o => o.RoundId == locked.Id && o.UserId == joao);
        Assert.False(stored.IsAbsent);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "SeasonRecalculated");
        Assert.Contains(db.AuditLogs, a => a.Action == "ParticipantAbsencesReviewed");

        // The scored round was not replayed: João is still absent there.
        Assert.True(db.RoundParticipantResults.Single(r => r.RoundId == scored.Id && r.UserId == joao).WasAbsent);
    }

    [Fact]
    public async Task Reviewing_with_no_changes_is_a_no_op()
    {
        using var db = CreateContext();
        var joao = CreateParticipant(db, "João Paulo");
        var bruno = CreateParticipant(db, "Bruno");
        var scored = await ScoredRound(db, 1, bruno);

        // Confirming the dialog with every round still ticked as absent.
        var result = await TestServices.RoundScoring(db).ReviewParticipantAbsencesAsync(
            joao, Review("Conferido.", (scored.Id, true)), Admin, Ct);

        Assert.Equal(0, result.ChangedRounds);
        Assert.False(result.Recalculated);
        db.ChangeTracker.Clear();
        Assert.Empty(db.AbsenceOverrides);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "ParticipantAbsencesReviewed");
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "SeasonRecalculated");
    }

    [Fact]
    public async Task Reviewing_rolls_back_the_overrides_when_the_recalculation_fails()
    {
        using var db = CreateContext();
        var joao = CreateParticipant(db, "João Paulo");
        var bruno = CreateParticipant(db, "Bruno");
        var first = await ScoredRound(db, 1, bruno);
        var second = await ScoredRound(db, 2, bruno);

        // A result wiped after scoring makes the replay of round 2 fail half-way, after round 1
        // was already re-scored inside the transaction.
        db.RoundMatches.Single(m => m.RoundId == second.Id).HomeScore = null;
        db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => TestServices.RoundScoring(db)
            .ReviewParticipantAbsencesAsync(joao, Review("Ainda não participava.", (first.Id, false)), Admin, Ct));
        db.ChangeTracker.Clear();

        // Nothing survived: not the override, not the round 1 replay, not the cleared ladder.
        Assert.Empty(db.AbsenceOverrides);
        Assert.Equal(2, db.Absences.Count(a => a.UserId == joao));
        Assert.True(db.RoundParticipantResults.Single(r => r.RoundId == first.Id && r.UserId == joao).WasAbsent);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "ParticipantAbsencesReviewed");
    }

    [Fact]
    public async Task Reviewing_an_ineligible_round_persists_nothing()
    {
        using var db = CreateContext();
        var joao = CreateParticipant(db, "João Paulo");
        var stillOpen = await PublishedRound(db, 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => TestServices.RoundScoring(db)
            .ReviewParticipantAbsencesAsync(joao, Review("Ainda não participava.", (stillOpen.Id, false)), Admin, Ct));
        db.ChangeTracker.Clear();

        Assert.Empty(db.AbsenceOverrides);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "ParticipantAbsencesReviewed");
    }
}
