using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Rounds;

public class RoundServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid ActingUser = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        // SQLite in-memory behaves like a real relational DB (proper insert/update
        // semantics, FK enforcement) — higher fidelity than the InMemory provider.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated(); // applies HasData seed (Big Seven + admin)

        // The active season is not part of the seed — add it for the tests.
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

    private static RoundService CreateService(AppDbContext db) => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static CreateMatchRequest Match(
        Guid home,
        Guid away,
        DateTime startsAt,
        Competition competition = Competition.PremierLeague,
        MatchPhase phase = MatchPhase.Regular,
        int? overrideMultiplier = null,
        string? justification = null)
        => new()
        {
            Competition = competition,
            Phase = phase,
            HomeTeamId = home,
            AwayTeamId = away,
            StartsAt = startsAt,
            ManualMultiplierOverride = overrideMultiplier,
            ManualMultiplierJustification = justification,
        };

    private static async Task<RoundDto> CreateDraftRound(RoundService service, int number = 1)
        => await service.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number, Title = "Rodada" }, ActingUser, Ct);

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Create_starts_as_draft()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var round = await CreateDraftRound(service);

        Assert.Equal(RoundStatus.Draft, round.Status);
        Assert.Null(round.PublishedAt);
        Assert.Null(round.FirstMatchStartsAt);
    }

    [Fact]
    public async Task Publish_round_with_matches_succeeds()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        await service.AddMatchAsync(round.Id, Match(SeedIds.Arsenal, SeedIds.Chelsea, new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc)), ActingUser, Ct);

        var published = await service.PublishAsync(round.Id, ActingUser, Ct);

        Assert.Equal(RoundStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAt);
    }

    [Fact]
    public async Task Publish_without_matches_is_rejected()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.PublishAsync(round.Id, ActingUser, Ct));

        Assert.Contains("pelo menos um jogo", ex.Message);
    }

    [Fact]
    public async Task Publish_computes_first_match_starts_at_from_earliest_match()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        var earliest = new DateTime(2025, 8, 9, 12, 30, 0, DateTimeKind.Utc);
        await service.AddMatchAsync(round.Id, Match(SeedIds.Arsenal, SeedIds.Chelsea, new DateTime(2025, 8, 10, 16, 0, 0, DateTimeKind.Utc)), ActingUser, Ct);
        await service.AddMatchAsync(round.Id, Match(SeedIds.Liverpool, SeedIds.Newcastle, earliest), ActingUser, Ct);
        await service.AddMatchAsync(round.Id, Match(SeedIds.Tottenham, SeedIds.ManchesterCity, new DateTime(2025, 8, 11, 18, 0, 0, DateTimeKind.Utc)), ActingUser, Ct);

        var published = await service.PublishAsync(round.Id, ActingUser, Ct);

        Assert.Equal(earliest, published.FirstMatchStartsAt);
    }

    [Fact]
    public async Task Lock_published_round_succeeds()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);
        await service.AddMatchAsync(round.Id, Match(SeedIds.Arsenal, SeedIds.Chelsea, new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc)), ActingUser, Ct);
        await service.PublishAsync(round.Id, ActingUser, Ct);

        var locked = await service.LockAsync(round.Id, ActingUser, Ct);

        Assert.Equal(RoundStatus.Locked, locked.Status);
        Assert.NotNull(locked.LockedAt);
    }

    [Fact]
    public async Task Cancel_round_succeeds()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        var cancelled = await service.CancelAsync(round.Id, ActingUser, Ct);

        Assert.Equal(RoundStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Cannot_add_match_to_locked_round_without_override()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);
        await service.AddMatchAsync(round.Id, Match(SeedIds.Arsenal, SeedIds.Chelsea, new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc)), ActingUser, Ct);
        await service.PublishAsync(round.Id, ActingUser, Ct);
        await service.LockAsync(round.Id, ActingUser, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddMatchAsync(round.Id, Match(SeedIds.Liverpool, SeedIds.Newcastle, new DateTime(2025, 8, 12, 14, 0, 0, DateTimeKind.Utc)), ActingUser, Ct));

        Assert.Contains("bloqueada", ex.Message);
    }

    [Fact]
    public async Task Cannot_add_match_with_same_home_and_away()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddMatchAsync(round.Id, Match(SeedIds.Arsenal, SeedIds.Arsenal, new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc)), ActingUser, Ct));

        Assert.Contains("mesmo time", ex.Message);
    }

    [Fact]
    public async Task Cannot_add_second_league_one_match_without_justified_override()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        await service.AddMatchAsync(round.Id,
            Match(SeedIds.Arsenal, SeedIds.Chelsea, new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc), Competition.LeagueOne),
            ActingUser, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddMatchAsync(round.Id,
                Match(SeedIds.Liverpool, SeedIds.Newcastle, new DateTime(2025, 8, 11, 14, 0, 0, DateTimeKind.Utc), Competition.LeagueOne),
                ActingUser, Ct));

        Assert.Contains("League One", ex.Message);
    }

    [Fact]
    public async Task Can_add_second_league_one_match_with_justified_override()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateDraftRound(service);

        await service.AddMatchAsync(round.Id,
            Match(SeedIds.Arsenal, SeedIds.Chelsea, new DateTime(2025, 8, 10, 14, 0, 0, DateTimeKind.Utc), Competition.LeagueOne),
            ActingUser, Ct);

        var second = await service.AddMatchAsync(round.Id,
            Match(SeedIds.Liverpool, SeedIds.Newcastle, new DateTime(2025, 8, 11, 14, 0, 0, DateTimeKind.Utc), Competition.LeagueOne,
                overrideMultiplier: 2, justification: "Rodada especial com dois jogos da League One."),
            ActingUser, Ct);

        Assert.Equal(Competition.LeagueOne, second.Competition);
        Assert.Equal(2, second.ManualMultiplierOverride);

        var roundDto = await service.GetByIdAsync(round.Id, Ct);
        Assert.Equal(2, roundDto.Matches.Count(m => m.Competition == Competition.LeagueOne));
    }
}
