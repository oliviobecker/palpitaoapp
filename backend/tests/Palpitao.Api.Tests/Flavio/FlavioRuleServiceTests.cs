using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Flavio;
using Xunit;

namespace Palpitao.Api.Tests.Flavio;

public class FlavioRuleServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly CancellationToken Ct = CancellationToken.None;

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
        db.SaveChanges();
        return id;
    }

    private static Round InsertPublishedRound(
        AppDbContext db, int number, DateTime publishedAt, DateTime firstMatchStartsAt, DateTime? mirrorPublishedAt = null)
    {
        var round = new Round
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            Number = number,
            Status = RoundStatus.Published,
            PublishedAt = publishedAt,
            MirrorPublishedAt = mirrorPublishedAt,
            FirstMatchStartsAt = firstMatchStartsAt,
            CreatedByUserId = SeedIds.AdminUser,
            CreatedAt = DateTime.UtcNow,
        };
        db.Rounds.Add(round);
        db.RoundMatches.Add(new RoundMatch
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = firstMatchStartsAt,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return round;
    }

    private static void InsertPrediction(AppDbContext db, Round round, Guid userId, DateTime submittedAt)
    {
        var matchId = db.RoundMatches.First(m => m.RoundId == round.Id).Id;
        db.Predictions.Add(new Prediction
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            RoundMatchId = matchId,
            UserId = userId,
            PredictedHomeScore = 1,
            PredictedAwayScore = 0,
            SubmittedAt = submittedAt,
        });
        db.SaveChanges();
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void Does_not_apply_before_round_16()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);

        Assert.False(service.AppliesToRound(15));
        Assert.True(service.AppliesToRound(16));
    }

    [Fact]
    public async Task Identifies_leader_before_the_round()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");
        var other = CreateParticipant(db, "Outro");

        db.Standings.Add(new Standing { Id = Guid.NewGuid(), SeasonId = SeasonId, UserId = leader, TotalPoints = 100, Position = 1, UpdatedAt = DateTime.UtcNow });
        db.Standings.Add(new Standing { Id = Guid.NewGuid(), SeasonId = SeasonId, UserId = other, TotalPoints = 50, Position = 2, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var leaders = await service.GetLeadersBeforeRoundAsync(SeasonId, Ct);

        Assert.Equal(new[] { leader }, leaders);
    }

    [Fact]
    public async Task Leader_within_deadline_is_not_penalized()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");

        var published = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var firstMatch = published.AddHours(48); // gap >= 24h -> 24h window
        var round = InsertPublishedRound(db, 16, published, firstMatch);

        InsertPrediction(db, round, leader, published.AddHours(1)); // within 24h

        Assert.False(await service.ShouldPenalizeLeaderAsync(round.Id, leader, Ct));
    }

    [Fact]
    public async Task Leader_after_deadline_is_penalized()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");

        var published = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var firstMatch = published.AddHours(48); // 24h window, deadline = published + 24h
        var round = InsertPublishedRound(db, 16, published, firstMatch);

        InsertPrediction(db, round, leader, published.AddHours(30)); // after 24h, before lock

        Assert.True(await service.ShouldPenalizeLeaderAsync(round.Id, leader, Ct));
    }

    [Fact]
    public async Task Leader_without_predictions_is_treated_as_absence()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");

        var published = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var round = InsertPublishedRound(db, 16, published, published.AddHours(48));

        // No predictions inserted -> not penalized by Flávio (handled as absence).
        Assert.False(await service.ShouldPenalizeLeaderAsync(round.Id, leader, Ct));
    }

    [Theory]
    [InlineData(17, 8)]
    [InlineData(16, 8)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void Half_penalty_rounds_down(int gross, int expected)
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);

        Assert.Equal(expected, service.ApplyHalfPenalty(gross));
    }

    [Fact]
    public void Short_notice_publication_uses_12h_window()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);

        var published = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var firstMatch = published.AddHours(10); // published < 24h before first match
        var round = InsertPublishedRound(db, 16, published, firstMatch);

        var deadline = service.ComputeSpecialDeadline(round);

        Assert.Equal(12, deadline.WindowHours);
        // 12h window would land after the first match -> general lock prevails.
        Assert.True(deadline.Conflict);
        Assert.Equal(firstMatch, deadline.EffectiveDeadlineUtc);
    }
}
