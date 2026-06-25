using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Standings;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

public class RoundScoringServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    private sealed record Kit(
        RoundScoringService Scoring,
        RoundService Rounds,
        PredictionsService Predictions,
        StandingsService Standings);

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

    private static Kit Build(AppDbContext db)
    {
        var audit = new AuditService(db);
        var current = new FakeCurrentGroupService();
        var standings = new StandingsService(db, current);
        var scoringConfig = new SeasonScoringConfigService(db, audit, current);
        var scoring = new RoundScoringService(
            db, new ScoringService(), scoringConfig, new AbsenceService(db, audit, current), new FlavioRuleService(db), standings, audit, current);
        return new Kit(scoring, new RoundService(db, audit, current), new PredictionsService(db, audit, current), standings);
    }

    private static Guid CreateParticipant(AppDbContext db, string name = "Participante")
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

    private static readonly (Guid Home, Guid Away)[] Pairs =
    {
        (SeedIds.Arsenal, SeedIds.Chelsea),
        (SeedIds.Liverpool, SeedIds.Newcastle),
    };

    private static async Task<RoundDto> PublishedRound(
        Kit kit, int number, params (Competition Competition, MatchPhase Phase)[] specs)
    {
        if (specs.Length == 0)
        {
            specs = new[] { (Competition.Championship, MatchPhase.Regular) };
        }

        var round = await kit.Rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number }, Admin, Ct);
        for (var i = 0; i < specs.Length; i++)
        {
            await kit.Rounds.AddMatchAsync(round.Id, new CreateMatchRequest
            {
                Competition = specs[i].Competition,
                Phase = specs[i].Phase,
                HomeTeamId = Pairs[i].Home,
                AwayTeamId = Pairs[i].Away,
                StartsAt = Future.AddHours(i),
            }, Admin, Ct);
        }

        return await kit.Rounds.PublishAsync(round.Id, Admin, Ct);
    }

    private static async Task SavePredictions(Kit kit, RoundDto round, Guid user, params (int Home, int Away)[] scores)
    {
        var items = round.Matches.Select((m, i) => new PredictionItemRequest
        {
            RoundMatchId = m.Id,
            PredictedHomeScore = scores[i].Home,
            PredictedAwayScore = scores[i].Away,
        }).ToList();
        await kit.Predictions.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest { Predictions = items }, false, Ct);
    }

    private static async Task SetResults(Kit kit, RoundDto round, params (int Home, int Away)[] results)
    {
        for (var i = 0; i < round.Matches.Count; i++)
        {
            await kit.Scoring.SetMatchResultAsync(round.Matches[i].Id,
                new MatchResultRequest { HomeScore = results[i].Home, AwayScore = results[i].Away }, Admin, Ct);
        }
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Cannot_score_without_all_results()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var round = await PublishedRound(kit, 1);
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct));
        Assert.Contains("resultado de todos os jogos", ex.Message);
    }

    [Fact]
    public async Task Scores_correct_column_points()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(kit, 1); // Championship, multiplier 1
        await SavePredictions(kit, round, user, (1, 0)); // home win
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (2, 1)); // home win, different score

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var p = results.Participants.Single(x => x.UserId == user);
        Assert.Equal(1, p.FinalPoints);
        Assert.True(p.MatchScores[0].IsCorrectColumn);
        Assert.False(p.MatchScores[0].IsExactScore);
    }

    [Fact]
    public async Task Scores_exact_score_points()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(kit, 1); // Championship, multiplier 1
        await SavePredictions(kit, round, user, (2, 1));
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (2, 1)); // exact, Traditional = 3

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var p = results.Participants.Single(x => x.UserId == user);
        Assert.Equal(3, p.FinalPoints);
        Assert.Equal(ScoreCategory.Traditional, p.MatchScores[0].ScoreCategory);
        Assert.True(p.MatchScores[0].IsExactScore);
    }

    [Fact]
    public async Task Applies_multiplier_x2()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        // Premier League Big Seven derby -> x2.
        var round = await PublishedRound(kit, 1, (Competition.PremierLeague, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (2, 1));
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (2, 1)); // exact Traditional 3 * 2 = 6

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var p = results.Participants.Single(x => x.UserId == user);
        Assert.Equal(2, p.MatchScores[0].Multiplier);
        Assert.Equal(6, p.FinalPoints);
    }

    [Fact]
    public async Task Round_results_expose_audit_flags_for_a_classic()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        // Arsenal x Chelsea — both Big Seven, so a classic with multiplier x2.
        var round = await PublishedRound(kit, 1, (Competition.PremierLeague, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (2, 1));
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (2, 1));

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var match = results.Matches.Single();
        Assert.True(match.IsClassic);
        Assert.False(match.IsManualMultiplier);
        Assert.Equal(2, match.Multiplier);
    }

    [Fact]
    public async Task Applies_multiplier_x3()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        // FA Cup final -> x3.
        var round = await PublishedRound(kit, 1, (Competition.FACup, MatchPhase.FACupFinal));
        await SavePredictions(kit, round, user, (1, 0));
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (1, 0)); // exact Traditional 3 * 3 = 9

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var p = results.Participants.Single(x => x.UserId == user);
        Assert.Equal(3, p.MatchScores[0].Multiplier);
        Assert.Equal(9, p.FinalPoints);
    }

    [Fact]
    public async Task Absent_participant_scores_zero()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var present = CreateParticipant(db, "Presente");
        var absent = CreateParticipant(db, "Ausente");
        var round = await PublishedRound(kit, 1);
        await SavePredictions(kit, round, present, (1, 0));
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (1, 0));

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var a = results.Participants.Single(x => x.UserId == absent);
        Assert.True(a.WasAbsent);
        Assert.Equal(0, a.FinalPoints);
    }

    [Fact]
    public async Task Third_absence_removes_20_from_standings()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 3; i++)
        {
            var round = await PublishedRound(kit, i);
            await kit.Rounds.LockAsync(round.Id, Admin, Ct);
            await SetResults(kit, round, (2, 1));
            await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);
        }

        var standings = await kit.Standings.GetStandingsAsync(SeasonId, Ct);
        var row = standings.Single(s => s.UserId == user);
        Assert.Equal(20, row.PenaltyPoints);
        Assert.Equal(-20, row.TotalPoints);
    }

    [Fact]
    public async Task Fifth_absence_marks_participant_eliminated()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 5; i++)
        {
            var round = await PublishedRound(kit, i);
            await kit.Rounds.LockAsync(round.Id, Admin, Ct);
            await SetResults(kit, round, (2, 1));
            await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));
        var standings = await kit.Standings.GetStandingsAsync(SeasonId, Ct);
        Assert.True(standings.Single(s => s.UserId == user).IsEliminated);
    }

    [Fact]
    public async Task Flavio_rule_halves_round_points()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var leader = CreateParticipant(db, "Líder");

        // Make the participant the standing leader before the round.
        db.Standings.Add(new Standing { Id = Guid.NewGuid(), SeasonId = SeasonId, UserId = leader, TotalPoints = 100, Position = 1, UpdatedAt = DateTime.UtcNow });

        var published = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var firstMatch = published.AddHours(48); // 24h window, deadline = published + 24h
        var round = new Round
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            Number = 16,
            Status = RoundStatus.Locked,
            PublishedAt = published,
            FirstMatchStartsAt = firstMatch,
            LockedAt = firstMatch,
            CreatedByUserId = Admin,
            CreatedAt = published,
        };
        db.Rounds.Add(round);
        var match = new RoundMatch
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            Competition = Competition.Championship, // multiplier 1
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = firstMatch,
            HomeScore = 5,
            AwayScore = 0,
            IsFinished = true,
            CreatedAt = published,
        };
        db.RoundMatches.Add(match);
        db.Predictions.Add(new Prediction
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            RoundMatchId = match.Id,
            UserId = leader,
            PredictedHomeScore = 5,
            PredictedAwayScore = 0, // exact 5x0 = ExtraUncommon (10)
            SubmittedAt = published.AddHours(30), // after 24h deadline, before lock
        });
        db.SaveChanges();

        var results = await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);

        var p = results.Participants.Single(x => x.UserId == leader);
        Assert.Equal(10, p.GrossPoints);
        Assert.Equal(5, p.FinalPoints); // halved
        Assert.True(p.FlavioRuleApplied);
    }

    [Fact]
    public async Task Recalculating_season_is_idempotent()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var a = CreateParticipant(db, "Ana");
        var b = CreateParticipant(db, "Bruno");

        // Round 1
        var r1 = await PublishedRound(kit, 1);
        await SavePredictions(kit, r1, a, (2, 1)); // exact -> 3
        await SavePredictions(kit, r1, b, (1, 0)); // column -> 1
        await kit.Rounds.LockAsync(r1.Id, Admin, Ct);
        await SetResults(kit, r1, (2, 1));
        await kit.Scoring.ScoreRoundAsync(r1.Id, Admin, Ct);

        // Round 2
        var r2 = await PublishedRound(kit, 2);
        await SavePredictions(kit, r2, a, (0, 0)); // exact 0x0 Medium -> 5
        await SavePredictions(kit, r2, b, (1, 0)); // home win vs 0x0 draw -> wrong -> 0
        await kit.Rounds.LockAsync(r2.Id, Admin, Ct);
        await SetResults(kit, r2, (0, 0));
        await kit.Scoring.ScoreRoundAsync(r2.Id, Admin, Ct);

        var before = await kit.Standings.GetStandingsAsync(SeasonId, Ct);

        await kit.Scoring.RecalculateSeasonAsync(SeasonId, Admin, Ct);
        var after = await kit.Standings.GetStandingsAsync(SeasonId, Ct);

        Assert.Equal(
            before.Select(s => (s.Position, s.UserId, s.TotalPoints)),
            after.Select(s => (s.Position, s.UserId, s.TotalPoints)));

        // Ana: 3 + 5 = 8, Bruno: 1 + 0 = 1
        Assert.Equal(a, after[0].UserId);
        Assert.Equal(8, after[0].TotalPoints);
        Assert.Equal(1, after[1].TotalPoints);
    }
}
