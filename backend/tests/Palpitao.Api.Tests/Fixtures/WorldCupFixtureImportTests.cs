using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Fixtures;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Fixtures;

public class WorldCupFixtureImportTests
{
    private static readonly Guid WcGroup = Guid.Parse("55555555-5555-5555-5555-555555555501");
    private static readonly Guid WcSeason = Guid.Parse("55555555-5555-5555-5555-555555555502");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class FakeFixtureProvider : IFixtureProvider
    {
        public string SourceName => "FakeSource";
        public Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
            DateTime s, DateTime e, IReadOnlyList<Competition> c, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<FixtureCandidateDto>>(new List<FixtureCandidateDto>());
    }

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
            CreatedByUserId = Admin,
            OwnerUserId = Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Seasons.Add(new Season { Id = WcSeason, GroupId = WcGroup, Name = "WC", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return db;
    }

    private static FixtureImportService CreateService(AppDbContext db)
        => new(db, new FakeFixtureProvider(), new ScoringService(), new AuditService(db),
            new FakeCurrentGroupService(WcGroup), Options.Create(new FixtureOptions { EnableExternalFixtureImport = true }));

    private static Guid CreateDraftRound(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Rounds.Add(new Round
        {
            Id = id, GroupId = WcGroup, SeasonId = WcSeason, Number = 1,
            Status = RoundStatus.Draft, CreatedByUserId = Admin, CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    private static ImportFixtureItem Item(string home, string away, Competition competition, MatchPhase phase)
        => new()
        {
            ExternalId = $"{home}-{away}",
            Competition = competition,
            Phase = phase,
            HomeTeamName = home,
            AwayTeamName = away,
            StartsAt = new DateTime(2026, 6, 24, 22, 0, 0, DateTimeKind.Utc),
            Source = "FakeSource",
        };

    [Fact]
    public async Task Import_creates_a_national_team_for_a_new_nation()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);

        // Brazil is seeded (champion); Scotland is new -> created as a national team.
        var response = await CreateService(db).ImportAsync(roundId,
            new ImportFixturesRequest { Fixtures = { Item("Brazil", "Scotland", Competition.FifaWorldCup, MatchPhase.WorldCupGroupStage) } },
            Admin, Ct);

        Assert.Equal(1, response.ImportedCount);
        var scotland = db.Teams.Single(t => t.Name == "Scotland");
        Assert.Equal(TeamType.NationalTeam, scotland.TeamType);
        Assert.False(scotland.IsWorldChampion); // unknown titles -> 0
    }

    [Fact]
    public async Task Import_rejects_an_english_competition_in_a_world_cup_group()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(db).ImportAsync(roundId,
            new ImportFixturesRequest { Fixtures = { Item("Arsenal", "Chelsea", Competition.PremierLeague, MatchPhase.Regular) } },
            Admin, Ct));

        Assert.Equal("tournament.competitionNotAllowed", ex.Key);
    }
}
