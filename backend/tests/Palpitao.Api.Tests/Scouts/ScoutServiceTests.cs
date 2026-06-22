using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Controllers;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Scouts;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Scouts;

public class ScoutServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("44444444-4444-4444-4444-444444444401");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    private static readonly (Guid Home, Guid Away)[] Pairs =
    {
        (SeedIds.Arsenal, SeedIds.Chelsea),
        (SeedIds.Liverpool, SeedIds.Newcastle),
    };

    private sealed record Kit(ScoutService Scout, RoundService Rounds, PredictionsService Predictions);

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
        return new Kit(new ScoutService(db, current), new RoundService(db, audit, current), new PredictionsService(db, audit, current));
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
        TestSeed.AddDefaultGroupMembership(db, id);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> PublishedRound(Kit kit, int number, int matches = 1)
    {
        var round = await kit.Rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number }, Admin, Ct);
        for (var i = 0; i < matches; i++)
        {
            await kit.Rounds.AddMatchAsync(round.Id, new CreateMatchRequest
            {
                Competition = Competition.PremierLeague,
                Phase = MatchPhase.Regular,
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

    [Fact]
    public void Controller_requires_group_admin()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(
            typeof(AdminScoutController), typeof(Palpitao.Api.Auth.RequireGroupAdminAttribute)));
    }

    [Fact]
    public async Task Scout_throws_when_round_not_found()
    {
        using var db = CreateContext();
        var kit = Build(db);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Scout.GetRoundScoutAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Scout_groups_participants_by_scoreline()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var round = await PublishedRound(kit, 1);
        var alice = CreateParticipant(db, "Alice");
        var bob = CreateParticipant(db, "Bob");
        var carol = CreateParticipant(db, "Carol");
        await SavePredictions(kit, round, alice, (2, 0));
        await SavePredictions(kit, round, bob, (2, 0));
        await SavePredictions(kit, round, carol, (1, 1));

        var scout = await kit.Scout.GetRoundScoutAsync(round.Id, Ct);

        var match = Assert.Single(scout.Matches);
        Assert.Equal(2, match.Groups.Count);
        // Ordered by home then away score: 1x1 before 2x0.
        Assert.Equal((1, 1), (match.Groups[0].HomeScore, match.Groups[0].AwayScore));
        Assert.Equal(new[] { "Carol" }, match.Groups[0].Names);
        Assert.Equal((2, 0), (match.Groups[1].HomeScore, match.Groups[1].AwayScore));
        Assert.Equal(new[] { "Alice", "Bob" }, match.Groups[1].Names);
    }

    [Fact]
    public async Task Scout_sorts_names_alphabetically_within_a_group()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var round = await PublishedRound(kit, 1);
        var zoe = CreateParticipant(db, "Zoe");
        var ana = CreateParticipant(db, "Ana");
        await SavePredictions(kit, round, zoe, (3, 1));
        await SavePredictions(kit, round, ana, (3, 1));

        var scout = await kit.Scout.GetRoundScoutAsync(round.Id, Ct);

        var group = Assert.Single(Assert.Single(scout.Matches).Groups);
        Assert.Equal(new[] { "Ana", "Zoe" }, group.Names);
    }

    [Fact]
    public async Task Scout_returns_match_with_empty_groups_when_no_predictions()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var round = await PublishedRound(kit, 1, matches: 2);

        var scout = await kit.Scout.GetRoundScoutAsync(round.Id, Ct);

        Assert.Equal(2, scout.Matches.Count);
        Assert.All(scout.Matches, m => Assert.Empty(m.Groups));
        Assert.Equal(1, scout.RoundNumber);
    }
}
