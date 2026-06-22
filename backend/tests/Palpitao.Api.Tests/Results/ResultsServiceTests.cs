using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Controllers;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Results;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Standings;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Results;

public class ResultsServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    private static readonly (Guid Home, Guid Away)[] Pairs =
    {
        (SeedIds.Arsenal, SeedIds.Chelsea),
        (SeedIds.Liverpool, SeedIds.Newcastle),
        (SeedIds.ManchesterCity, SeedIds.ManchesterUnited),
    };

    private sealed class FakeResultsProvider : IResultsProvider
    {
        public string Name => "Fake";
        public bool IsEnabled { get; set; }
        public List<ExternalMatchResultDto> Results { get; } = new();
        public Exception? ThrowOnFetch { get; set; }

        public Task<IReadOnlyList<ExternalMatchResultDto>> GetResultsForRoundAsync(Round round, CancellationToken ct)
        {
            if (ThrowOnFetch is not null)
            {
                throw ThrowOnFetch;
            }

            return Task.FromResult<IReadOnlyList<ExternalMatchResultDto>>(Results);
        }
    }

    private sealed record Kit(
        ResultsUpdateService Refresh,
        TemporaryStandingsService Temp,
        RoundScoringService Scoring,
        RoundService Rounds,
        PredictionsService Predictions,
        FakeResultsProvider Provider);

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

    private static Kit Build(AppDbContext db, bool providerEnabled = false)
    {
        var audit = new AuditService(db);
        var current = new FakeCurrentGroupService();
        var scoring = new ScoringService();
        var standings = new StandingsService(db, current);
        var provider = new FakeResultsProvider { IsEnabled = providerEnabled };
        return new Kit(
            new ResultsUpdateService(db, provider, audit, current),
            new TemporaryStandingsService(db, scoring, current),
            new RoundScoringService(db, scoring, new AbsenceService(db, audit, current), new FlavioRuleService(db), standings, audit, current),
            new RoundService(db, audit, current),
            new PredictionsService(db, audit, current),
            provider);
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

    private static async Task SetLive(AppDbContext db, Guid matchId, MatchStatus status, int? home = null, int? away = null)
    {
        var m = await db.RoundMatches.FirstAsync(x => x.Id == matchId);
        m.Status = status;
        m.HomeScore = home;
        m.AwayScore = away;
        m.IsFinished = status == MatchStatus.Finished;
        m.LastResultUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Refresh
    // -----------------------------------------------------------------------
    [Fact]
    public void Controller_requires_group_admin()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(
            typeof(AdminResultsController), typeof(Palpitao.Api.Auth.RequireGroupAdminAttribute)));
    }

    [Fact]
    public async Task Refresh_disabled_provider_reports_not_enabled_and_keeps_status()
    {
        using var db = CreateContext();
        var kit = Build(db, providerEnabled: false);
        var round = await PublishedRound(kit, 1);

        var response = await kit.Refresh.RefreshAsync(round.Id, Admin, Ct);

        Assert.False(response.ProviderEnabled);
        Assert.Equal(0, response.UpdatedMatches);
        var reloaded = await db.Rounds.FirstAsync(r => r.Id == round.Id);
        Assert.Equal(RoundStatus.Published, reloaded.Status);
        Assert.NotNull(reloaded.ResultsUpdatedAt);
    }

    [Fact]
    public async Task Refresh_not_allowed_on_cancelled_round()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var round = await PublishedRound(kit, 1);
        await kit.Rounds.CancelAsync(round.Id, Admin, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => kit.Refresh.RefreshAsync(round.Id, Admin, Ct));
        Assert.Equal("results.roundCancelled", ex.Key);
    }

    [Fact]
    public async Task Refresh_does_not_change_status_to_scored()
    {
        using var db = CreateContext();
        var kit = Build(db, providerEnabled: true);
        var round = await PublishedRound(kit, 1);
        await kit.Rounds.LockAsync(round.Id, Admin, Ct);

        await kit.Refresh.RefreshAsync(round.Id, Admin, Ct);

        var reloaded = await db.Rounds.FirstAsync(r => r.Id == round.Id);
        Assert.Equal(RoundStatus.Locked, reloaded.Status);
    }

    [Fact]
    public async Task Refresh_enabled_provider_applies_results_to_matches()
    {
        using var db = CreateContext();
        var kit = Build(db, providerEnabled: true);
        var round = await PublishedRound(kit, 1, (Competition.PremierLeague, MatchPhase.Regular));
        kit.Provider.Results.Add(new ExternalMatchResultDto
        {
            HomeTeamName = "Arsenal",
            AwayTeamName = "Chelsea",
            HomeScore = 2,
            AwayScore = 1,
            Status = MatchStatus.Finished,
        });

        var response = await kit.Refresh.RefreshAsync(round.Id, Admin, Ct);

        Assert.Equal(1, response.UpdatedMatches);
        Assert.Equal(1, response.FinishedMatches);
        var match = await db.RoundMatches.FirstAsync(m => m.RoundId == round.Id);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(2, match.HomeScore);
        Assert.True(match.IsFinished);
    }

    [Fact]
    public async Task Refresh_audits_the_action()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var round = await PublishedRound(kit, 1);

        await kit.Refresh.RefreshAsync(round.Id, Admin, Ct);

        Assert.Contains(db.AuditLogs, a => a.Action == "ResultsRefreshed");
    }

    [Fact]
    public async Task Refresh_does_not_change_official_standings()
    {
        using var db = CreateContext();
        var kit = Build(db, providerEnabled: true);
        var user = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1, (Competition.PremierLeague, MatchPhase.Regular));
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            UserId = user,
            TotalPoints = 100,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        kit.Provider.Results.Add(new ExternalMatchResultDto
        {
            HomeTeamName = "Arsenal",
            AwayTeamName = "Chelsea",
            HomeScore = 2,
            AwayScore = 1,
            Status = MatchStatus.Finished,
        });

        await kit.Refresh.RefreshAsync(round.Id, Admin, Ct);

        var standing = await db.Standings.FirstAsync(s => s.UserId == user);
        Assert.Equal(100, standing.TotalPoints);
    }

    // -----------------------------------------------------------------------
    // Temporary standings
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Temporary_ignores_not_started_matches()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1,
            (Competition.Championship, MatchPhase.Regular),
            (Competition.Championship, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (1, 0), (2, 0));
        // First match finished, second not started.
        await SetLive(db, round.Matches[0].Id, MatchStatus.Finished, 1, 0);
        await SetLive(db, round.Matches[1].Id, MatchStatus.NotStarted);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(1, temp.ComputedMatches);
        Assert.Equal(1, temp.RemainingMatches);
        Assert.Equal(3, temp.Standings.Single().RoundTemporaryPoints); // 1x0 exact = 3
    }

    [Fact]
    public async Task Temporary_uses_in_progress_current_score()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1, (Competition.Championship, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (1, 0));
        await SetLive(db, round.Matches[0].Id, MatchStatus.InProgress, 1, 0);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(1, temp.ComputedMatches);
        Assert.Equal(3, temp.Standings.Single().RoundTemporaryPoints);
    }

    [Fact]
    public async Task Temporary_uses_finished_final_score()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1, (Competition.Championship, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (2, 2));
        await SetLive(db, round.Matches[0].Id, MatchStatus.Finished, 2, 2);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(5, temp.Standings.Single().RoundTemporaryPoints); // 2x2 medium exact = 5
    }

    [Fact]
    public async Task Temporary_ignores_postponed_and_cancelled()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1,
            (Competition.Championship, MatchPhase.Regular),
            (Competition.Championship, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (1, 0), (1, 0));
        await SetLive(db, round.Matches[0].Id, MatchStatus.Postponed, 1, 0);
        await SetLive(db, round.Matches[1].Id, MatchStatus.Cancelled, 1, 0);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(0, temp.ComputedMatches);
        Assert.Equal(0, temp.RemainingMatches); // both dismissed
        Assert.Empty(temp.Standings);
    }

    [Fact]
    public async Task Temporary_applies_multipliers()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "João");
        // League One match -> multiplier x2.
        var round = await PublishedRound(kit, 1, (Competition.LeagueOne, MatchPhase.Regular));
        await SavePredictions(kit, round, user, (1, 0));
        await SetLive(db, round.Matches[0].Id, MatchStatus.Finished, 1, 0);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(6, temp.Standings.Single().RoundTemporaryPoints); // 3 base x2
    }

    [Fact]
    public async Task Temporary_does_not_apply_absence_penalty()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1,
            (Competition.Championship, MatchPhase.Regular),
            (Competition.Championship, MatchPhase.Regular));
        // Predicts only the first match (incomplete) -> would be "absent" officially.
        await SavePredictions(kit, round, user, (1, 0), (0, 0));
        await db.Predictions.Where(p => p.RoundMatchId == round.Matches[1].Id).ExecuteDeleteAsync(Ct);
        await SetLive(db, round.Matches[0].Id, MatchStatus.Finished, 1, 0);
        await SetLive(db, round.Matches[1].Id, MatchStatus.Finished, 0, 0);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        // Only the predicted match counts; no -20 penalty, points stay positive.
        Assert.Equal(3, temp.Standings.Single().RoundTemporaryPoints);
    }

    [Fact]
    public async Task Temporary_does_not_apply_flavio_rule()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db, "Líder");
        // Round 16 (Flávio applicable) and the user is the standings leader.
        var round = await PublishedRound(kit, 16, (Competition.Championship, MatchPhase.Regular));
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            UserId = user,
            TotalPoints = 50,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await SavePredictions(kit, round, user, (1, 0));
        await SetLive(db, round.Matches[0].Id, MatchStatus.Finished, 1, 0);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        var row = temp.Standings.Single();
        Assert.Equal(3, row.RoundTemporaryPoints); // full points, not halved
        Assert.Equal(50, row.CurrentOfficialTotalPoints);
        Assert.Equal(53, row.ProjectedTotalPoints);
    }

    [Fact]
    public async Task Temporary_orders_by_round_points()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var first = CreateParticipant(db, "Maria");
        var second = CreateParticipant(db, "João");
        var round = await PublishedRound(kit, 1, (Competition.Championship, MatchPhase.Regular));
        await SavePredictions(kit, round, first, (1, 0)); // exact -> 3
        await SavePredictions(kit, round, second, (3, 0)); // home win column only -> 1
        await SetLive(db, round.Matches[0].Id, MatchStatus.Finished, 1, 0);

        var temp = await kit.Temp.GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(2, temp.Standings.Count);
        Assert.Equal(first, temp.Standings[0].UserId);
        Assert.Equal(1, temp.Standings[0].Position);
        Assert.Equal(3, temp.Standings[0].RoundTemporaryPoints);
        Assert.Equal(2, temp.Standings[1].Position);
    }
}
