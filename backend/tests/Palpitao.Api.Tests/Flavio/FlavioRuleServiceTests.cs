using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Scoring;
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
    public void Does_not_apply_before_round_16_by_default()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);

        Assert.False(service.AppliesToRound(15, ScoringDefaults.FlavioFromRound));
        Assert.True(service.AppliesToRound(16, ScoringDefaults.FlavioFromRound));
    }

    [Fact]
    public void Honours_the_seasons_configured_threshold()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);

        // A season that starts the rule at round 10 penalizes from 10 on, not from 16.
        Assert.False(service.AppliesToRound(9, 10));
        Assert.True(service.AppliesToRound(10, 10));
        Assert.True(service.AppliesToRound(15, 10));
    }

    [Fact]
    public async Task Configured_threshold_drives_the_penalty()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");

        var published = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var round = InsertPublishedRound(db, 10, published, published.AddHours(48));
        InsertPrediction(db, round, leader, published.AddHours(30)); // late

        Assert.False(await service.ShouldPenalizeLeaderAsync(round.Id, leader, 16, Ct));
        Assert.True(await service.ShouldPenalizeLeaderAsync(round.Id, leader, 10, Ct));
    }

    [Fact]
    public async Task Identifies_leader_before_the_round()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");
        var other = CreateParticipant(db, "Outro");

        var published = DateTime.UtcNow.AddDays(-3);
        var prior = InsertPublishedRound(db, 4, published, published.AddHours(48));
        var current = InsertPublishedRound(db, 5, published, published.AddHours(48));
        var later = InsertPublishedRound(db, 6, published, published.AddHours(48));
        foreach (var (round, user, points, penalty) in new[] {
            (prior, leader, 100, 20), (prior, other, 70, 0),
            (current, other, 200, 0), (later, other, 300, 0) })
            db.RoundParticipantResults.Add(new RoundParticipantResult {
                Id = Guid.NewGuid(), SeasonId = SeasonId, RoundId = round.Id,
                UserId = user, FinalPoints = points, PenaltyPoints = penalty });
        db.Standings.Add(new Standing { Id = Guid.NewGuid(), SeasonId = SeasonId,
            UserId = other, TotalPoints = 570, Position = 1 });
        db.SaveChanges();

        var leaders = await service.GetLeadersBeforeRoundAsync(current.Id, Ct);

        Assert.Equal(new[] { leader }, leaders);

        // Only net points decide a tie; the current/future results still cannot break it.
        db.RoundParticipantResults.Single(r => r.RoundId == prior.Id && r.UserId == other).FinalPoints = 80;
        db.SaveChanges();
        Assert.Equal(new[] { leader, other }.Order(),
            (await service.GetLeadersBeforeRoundAsync(current.Id, Ct)).Order());
        Assert.Empty(await service.GetLeadersBeforeRoundAsync(prior.Id, Ct));
    }

    [Fact]
    public async Task Automatic_mode_does_not_force_a_penalty_at_the_deadline()
    {
        using var db = CreateContext();
        var leader = CreateParticipant(db, "Líder");
        var published = DateTime.UtcNow.AddDays(-3);
        var round = InsertPublishedRound(db, 5, published, published.AddHours(48));
        InsertPrediction(db, round, leader, published.AddHours(24));
        db.FlavioOverrides.Add(new FlavioOverride { Id = Guid.NewGuid(), RoundId = round.Id,
            UserId = leader, IsExempt = false, Justification = "Restaurado." });
        db.SaveChanges();
        Assert.False(await new FlavioRuleService(db).ShouldPenalizeLeaderAsync(round.Id, leader, 5, Ct));
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

        Assert.False(await service.ShouldPenalizeLeaderAsync(round.Id, leader, ScoringDefaults.FlavioFromRound, Ct));
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

        Assert.True(await service.ShouldPenalizeLeaderAsync(round.Id, leader, ScoringDefaults.FlavioFromRound, Ct));
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
        Assert.False(await service.ShouldPenalizeLeaderAsync(round.Id, leader, ScoringDefaults.FlavioFromRound, Ct));
    }

    [Fact]
    public async Task Leader_with_an_incomplete_late_set_is_still_penalized()
    {
        using var db = CreateContext();
        var service = new FlavioRuleService(db);
        var leader = CreateParticipant(db, "Líder");

        var published = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var firstMatch = published.AddHours(48); // gap >= 24h -> 24h window
        var round = InsertPublishedRound(db, 16, published, firstMatch);
        db.RoundMatches.Add(new RoundMatch
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Liverpool,
            AwayTeamId = SeedIds.Newcastle,
            StartsAt = firstMatch.AddHours(1),
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        InsertPrediction(db, round, leader, published.AddHours(30)); // 1 of 2, after 24h

        // Now that an incomplete set is no longer an absence, excusing it here would let the
        // leader dodge the halving by deliberately omitting one match — strictly better than
        // sending everything late. The completeness gate only covers "sent nothing".
        Assert.True(await service.ShouldPenalizeLeaderAsync(round.Id, leader, ScoringDefaults.FlavioFromRound, Ct));
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
