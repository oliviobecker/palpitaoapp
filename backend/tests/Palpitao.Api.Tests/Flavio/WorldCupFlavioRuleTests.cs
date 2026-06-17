using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Flavio;
using Xunit;

namespace Palpitao.Api.Tests.Flavio;

public class WorldCupFlavioRuleTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid WcGroup = Guid.Parse("66666666-6666-6666-6666-666666666601");
    private static readonly Guid WcSeason = Guid.Parse("66666666-6666-6666-6666-666666666602");

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        db.Groups.Add(new Group
        {
            Id = WcGroup,
            Name = "Copa",
            Slug = "copa",
            TournamentType = TournamentType.FifaWorldCup,
            CreatedByUserId = SeedIds.AdminUser,
            OwnerUserId = SeedIds.AdminUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Seasons.Add(new Season { Id = WcSeason, GroupId = WcGroup, Name = "WC", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return db;
    }

    private static Round RoundWith(params MatchPhase[] phases)
    {
        var round = new Round { Id = Guid.NewGuid(), Number = 3 };
        foreach (var phase in phases)
        {
            round.Matches.Add(new RoundMatch { Id = Guid.NewGuid(), Competition = Competition.FifaWorldCup, Phase = phase });
        }
        return round;
    }

    // --- Applicability (pure) ----------------------------------------------

    [Theory]
    [InlineData(MatchPhase.WorldCupGroupStage, false)]
    [InlineData(MatchPhase.WorldCupRoundOf32, false)]
    [InlineData(MatchPhase.WorldCupRoundOf16, false)]
    [InlineData(MatchPhase.WorldCupQuarterFinal, true)]
    [InlineData(MatchPhase.WorldCupSemiFinal, true)]
    [InlineData(MatchPhase.WorldCupThirdPlace, true)]
    [InlineData(MatchPhase.WorldCupFinal, true)]
    public void World_cup_rule_applies_only_from_the_quarter_finals(MatchPhase phase, bool expected)
    {
        var svc = new FlavioRuleService(CreateContext());
        Assert.Equal(expected, svc.ShouldApplyWorldCupFlavioRule(RoundWith(phase)));
        Assert.Equal(expected, svc.ShouldApplyFlavioRule(RoundWith(phase), TournamentType.FifaWorldCup));
    }

    [Fact]
    public void World_cup_rule_applies_when_any_match_is_a_knockout_from_quarters()
    {
        var svc = new FlavioRuleService(CreateContext());
        Assert.True(svc.ShouldApplyWorldCupFlavioRule(RoundWith(MatchPhase.WorldCupRoundOf16, MatchPhase.WorldCupQuarterFinal)));
    }

    // --- Penalty (DB-backed) ------------------------------------------------

    private Round InsertPublishedQuarterFinal(AppDbContext db, DateTime publishedAt, DateTime firstMatchStartsAt)
    {
        var home = db.Teams.Single(t => t.Name == "Brazil").Id;
        var away = db.Teams.Single(t => t.Name == "Germany").Id;
        var round = new Round
        {
            Id = Guid.NewGuid(),
            GroupId = WcGroup,
            SeasonId = WcSeason,
            Number = 3,
            Status = RoundStatus.Published,
            PublishedAt = publishedAt,
            FirstMatchStartsAt = firstMatchStartsAt,
            FlavioRuleApplies = true,
            CreatedByUserId = SeedIds.AdminUser,
            CreatedAt = DateTime.UtcNow,
        };
        round.Matches.Add(new RoundMatch
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            Competition = Competition.FifaWorldCup,
            Phase = MatchPhase.WorldCupQuarterFinal,
            HomeTeamId = home,
            AwayTeamId = away,
            StartsAt = firstMatchStartsAt,
        });
        db.Rounds.Add(round);
        db.SaveChanges();
        return round;
    }

    private static Guid AddLeaderWithPrediction(AppDbContext db, Round round, DateTime? submittedAt)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Name = "Leader", Email = $"{id}@x.com", PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow });
        if (submittedAt is DateTime at)
        {
            var match = round.Matches.First();
            db.Predictions.Add(new Prediction
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                RoundMatchId = match.Id,
                UserId = id,
                PredictedHomeScore = 1,
                PredictedAwayScore = 0,
                SubmittedAt = at,
            });
        }
        db.SaveChanges();
        return id;
    }

    [Fact]
    public async Task Leader_who_predicts_after_the_special_deadline_is_penalized()
    {
        using var db = CreateContext();
        var t0 = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var round = InsertPublishedQuarterFinal(db, publishedAt: t0, firstMatchStartsAt: t0.AddHours(48));
        // 24h window -> deadline t0+24h. Predicts at t0+30h (late, before first match).
        var leader = AddLeaderWithPrediction(db, round, t0.AddHours(30));

        Assert.True(await new FlavioRuleService(db).ShouldPenalizeLeaderAsync(round.Id, leader, Ct));
    }

    [Fact]
    public async Task Leader_who_predicts_within_the_deadline_is_not_penalized()
    {
        using var db = CreateContext();
        var t0 = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var round = InsertPublishedQuarterFinal(db, publishedAt: t0, firstMatchStartsAt: t0.AddHours(48));
        var leader = AddLeaderWithPrediction(db, round, t0.AddHours(10)); // on time

        Assert.False(await new FlavioRuleService(db).ShouldPenalizeLeaderAsync(round.Id, leader, Ct));
    }

    [Fact]
    public async Task Leader_who_does_not_predict_is_an_absence_not_a_flavio_penalty()
    {
        using var db = CreateContext();
        var t0 = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var round = InsertPublishedQuarterFinal(db, publishedAt: t0, firstMatchStartsAt: t0.AddHours(48));
        var leader = AddLeaderWithPrediction(db, round, submittedAt: null); // no predictions

        Assert.False(await new FlavioRuleService(db).ShouldPenalizeLeaderAsync(round.Id, leader, Ct));
    }
}
