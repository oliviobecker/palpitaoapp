using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Teams;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Fixtures;
using Palpitao.Api.Services.Teams;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Teams;

public class TeamCatalogServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>Returns the squad list each competition was configured with.</summary>
    private sealed class FakeTeamCatalogProvider : ITeamCatalogProvider
    {
        private readonly Dictionary<Competition, IReadOnlyList<string>> _rosters;

        public FakeTeamCatalogProvider(Dictionary<Competition, IReadOnlyList<string>> rosters)
        {
            _rosters = rosters;
        }

        public string SourceName => "FakeSource";

        public Exception? Throws { get; set; }

        public List<Competition> Requested { get; } = new();

        public Task<IReadOnlyList<string>> GetTeamNamesAsync(Competition competition, CancellationToken ct)
        {
            Requested.Add(competition);
            if (Throws is not null)
            {
                throw Throws;
            }

            return Task.FromResult(_rosters.TryGetValue(competition, out var names)
                ? names
                : (IReadOnlyList<string>)Array.Empty<string>());
        }
    }

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static TeamCatalogService CreateService(
        AppDbContext db, ITeamCatalogProvider provider, bool enabled = true)
        => new(
            db,
            provider,
            new AuditService(db),
            new FakeCurrentGroupService(),
            Options.Create(new FixtureOptions { EnableExternalFixtureImport = enabled }));

    private static Dictionary<Competition, IReadOnlyList<string>> Rosters(
        IEnumerable<string>? premierLeague = null,
        IEnumerable<string>? championship = null,
        IEnumerable<string>? leagueOne = null)
    {
        var map = new Dictionary<Competition, IReadOnlyList<string>>();
        if (premierLeague is not null)
        {
            map[Competition.PremierLeague] = premierLeague.ToList();
        }

        if (championship is not null)
        {
            map[Competition.Championship] = championship.ToList();
        }

        if (leagueOne is not null)
        {
            map[Competition.LeagueOne] = leagueOne.ToList();
        }

        return map;
    }

    private static List<string> SeededNames(AppDbContext db, Competition division)
        => db.Teams.Where(t => t.Division == division).Select(t => t.Name).ToList();

    private static TeamSyncCompetitionDto For(TeamSyncResponse response, Competition competition)
        => response.Competitions.Single(c => c.Competition == competition);

    // --- Preview -------------------------------------------------------------

    [Fact]
    public async Task Preview_matches_the_source_spellings_through_the_alias_map()
    {
        using var db = CreateContext();
        // "Spurs" and "Leeds" are how sources write clubs the catalogue stores in full.
        var roster = SeededNames(db, Competition.PremierLeague)
            .Where(n => n is not ("Tottenham" or "Leeds United"))
            .Concat(new[] { "Spurs", "Leeds" })
            .ToList();
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters(premierLeague: roster)));

        var response = await service.SyncAsync(apply: false, SeedIds.AdminUser, Ct);

        var pl = For(response, Competition.PremierLeague);
        Assert.Equal(20, pl.FoundCount);
        Assert.Equal(20, pl.UnchangedCount);
        Assert.Empty(pl.ToCreate);
        Assert.Empty(pl.ToMove);
        Assert.Empty(pl.NotFound);
    }

    [Fact]
    public async Task Preview_classifies_new_moved_and_missing_clubs()
    {
        using var db = CreateContext();
        var premierLeague = SeededNames(db, Competition.PremierLeague);
        var dropped = premierLeague[0];
        var promoted = db.Teams.First(t => t.Division == Competition.Championship).Name;

        var roster = premierLeague.Skip(1).Concat(new[] { promoted, "Barnsley Town Rovers" }).ToList();
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters(premierLeague: roster)));

        var response = await service.SyncAsync(apply: false, SeedIds.AdminUser, Ct);
        var pl = For(response, Competition.PremierLeague);

        var created = Assert.Single(pl.ToCreate);
        Assert.Equal("Barnsley Town Rovers", created.Name);
        Assert.Equal("BAR", created.ShortName);
        Assert.False(created.IsBigSevenClub);

        var moved = Assert.Single(pl.ToMove);
        Assert.Equal(promoted, moved.Name);
        Assert.Equal(Competition.Championship, moved.FromDivision);
        Assert.Equal(Competition.PremierLeague, moved.ToDivision);

        Assert.Equal(dropped, Assert.Single(pl.NotFound).Name);
    }

    [Fact]
    public async Task Preview_writes_nothing()
    {
        using var db = CreateContext();
        var before = db.Teams.Count();
        var service = CreateService(db, new FakeTeamCatalogProvider(
            Rosters(premierLeague: new[] { "Some Brand New Club" })));

        await service.SyncAsync(apply: false, SeedIds.AdminUser, Ct);

        Assert.Equal(before, db.Teams.Count());
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task A_competition_with_no_data_reports_nothing_as_missing()
    {
        using var db = CreateContext();
        // Off-season: the source knows nothing, which must not be read as "the
        // division is empty" — otherwise every club would show up as missing.
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        var response = await service.SyncAsync(apply: false, SeedIds.AdminUser, Ct);

        Assert.All(response.Competitions, c =>
        {
            Assert.Equal(0, c.FoundCount);
            Assert.Empty(c.NotFound);
            Assert.Empty(c.ToCreate);
            Assert.Empty(c.ToMove);
        });
    }

    [Fact]
    public async Task A_promoted_club_is_not_also_reported_missing_from_its_old_division()
    {
        using var db = CreateContext();
        var premierLeague = SeededNames(db, Competition.PremierLeague);
        var championship = SeededNames(db, Competition.Championship);
        var promoted = championship[0];

        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters(
            premierLeague: premierLeague.Concat(new[] { promoted }),
            championship: championship.Skip(1))));

        var response = await service.SyncAsync(apply: false, SeedIds.AdminUser, Ct);

        Assert.Equal(promoted, Assert.Single(For(response, Competition.PremierLeague).ToMove).Name);
        Assert.Empty(For(response, Competition.Championship).NotFound);
    }

    [Fact]
    public async Task National_teams_are_never_touched()
    {
        using var db = CreateContext();
        // "England" is a seeded national team; a source listing it as a club must not
        // move it into a division nor try to create a second row with the same name.
        var service = CreateService(db, new FakeTeamCatalogProvider(
            Rosters(premierLeague: new[] { "England" })));

        var response = await service.SyncAsync(apply: true, SeedIds.AdminUser, Ct);
        var pl = For(response, Competition.PremierLeague);

        Assert.Empty(pl.ToCreate);
        Assert.Empty(pl.ToMove);
        Assert.Null(db.Teams.Single(t => t.Name == "England").Division);
    }

    // --- Apply ---------------------------------------------------------------

    [Fact]
    public async Task Apply_creates_clubs_and_moves_divisions()
    {
        using var db = CreateContext();
        var promoted = db.Teams.First(t => t.Division == Competition.LeagueOne).Name;
        var championship = SeededNames(db, Competition.Championship);
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters(
            championship: championship.Concat(new[] { promoted, "Brand New FC" }))));

        var response = await service.SyncAsync(apply: true, SeedIds.AdminUser, Ct);

        Assert.True(response.Applied);
        var created = db.Teams.Single(t => t.Name == "Brand New FC");
        Assert.Equal(Competition.Championship, created.Division);
        Assert.Equal(TeamType.Club, created.TeamType);
        Assert.Equal("BRA", created.ShortName);
        Assert.Equal(Competition.Championship, db.Teams.Single(t => t.Name == promoted).Division);
    }

    [Fact]
    public async Task Apply_never_clears_the_division_of_a_club_missing_from_the_source()
    {
        using var db = CreateContext();
        var premierLeague = SeededNames(db, Competition.PremierLeague);
        var dropped = premierLeague[0];
        var service = CreateService(db, new FakeTeamCatalogProvider(
            Rosters(premierLeague: premierLeague.Skip(1))));

        var response = await service.SyncAsync(apply: true, SeedIds.AdminUser, Ct);

        Assert.Equal(dropped, Assert.Single(For(response, Competition.PremierLeague).NotFound).Name);
        // Reported, not removed: a gap in the source must never wipe the catalogue.
        Assert.Equal(Competition.PremierLeague, db.Teams.Single(t => t.Name == dropped).Division);
    }

    [Fact]
    public async Task Apply_audits_the_run_once_and_is_idempotent()
    {
        using var db = CreateContext();
        var championship = SeededNames(db, Competition.Championship);
        var provider = new FakeTeamCatalogProvider(Rosters(
            championship: championship.Concat(new[] { "Brand New FC" })));
        var service = CreateService(db, provider);

        await service.SyncAsync(apply: true, SeedIds.AdminUser, Ct);

        var entry = Assert.Single(db.AuditLogs.Where(a => a.Action == "TeamCatalogSynced"));
        Assert.Equal(SeedIds.DefaultGroup, entry.GroupId);
        Assert.Contains("Brand New FC", entry.Details);

        // Second run sees the club it just created: nothing left to do, nothing logged.
        var second = await service.SyncAsync(apply: true, SeedIds.AdminUser, Ct);
        Assert.All(second.Competitions, c =>
        {
            Assert.Empty(c.ToCreate);
            Assert.Empty(c.ToMove);
        });
        Assert.Single(db.AuditLogs.Where(a => a.Action == "TeamCatalogSynced"));
    }

    [Fact]
    public async Task Apply_with_an_empty_diff_writes_no_audit()
    {
        using var db = CreateContext();
        var service = CreateService(db, new FakeTeamCatalogProvider(
            Rosters(premierLeague: SeededNames(db, Competition.PremierLeague))));

        await service.SyncAsync(apply: true, SeedIds.AdminUser, Ct);

        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Sync_is_blocked_when_external_import_is_disabled()
    {
        using var db = CreateContext();
        var provider = new FakeTeamCatalogProvider(Rosters());
        var service = CreateService(db, provider, enabled: false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SyncAsync(apply: false, SeedIds.AdminUser, Ct));

        Assert.Equal("teams.syncDisabled", ex.Key);
        Assert.Empty(provider.Requested);
    }

    [Fact]
    public async Task A_provider_failure_leaves_the_catalogue_alone()
    {
        using var db = CreateContext();
        var before = db.Teams.Count();
        var provider = new FakeTeamCatalogProvider(Rosters())
        {
            Throws = new BusinessRuleException("teams.syncFailed"),
        };
        var service = CreateService(db, provider);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SyncAsync(apply: true, SeedIds.AdminUser, Ct));

        Assert.Equal(before, db.Teams.Count());
        Assert.Empty(db.AuditLogs);
    }

    // --- Manual move ---------------------------------------------------------

    [Fact]
    public async Task Moving_a_club_saves_it_and_audits_the_change()
    {
        using var db = CreateContext();
        var club = db.Teams.First(t => t.Division == Competition.LeagueOne);
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        var dto = await service.UpdateDivisionAsync(
            club.Id, new UpdateTeamDivisionRequest { Division = Competition.Championship },
            SeedIds.AdminUser, Ct);

        Assert.Equal(Competition.Championship, dto.Division);
        Assert.Equal(Competition.Championship, db.Teams.Single(t => t.Id == club.Id).Division);

        var entry = Assert.Single(db.AuditLogs.Where(a => a.Action == "TeamDivisionChanged"));
        Assert.Equal(SeedIds.DefaultGroup, entry.GroupId);
        // Names, not enum ordinals — the audit screen shows this raw.
        Assert.Contains("LeagueOne", entry.Details);
        Assert.Contains("Championship", entry.Details);
    }

    [Fact]
    public async Task Clearing_a_division_is_allowed()
    {
        using var db = CreateContext();
        var club = db.Teams.First(t => t.Division == Competition.LeagueOne);
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        var dto = await service.UpdateDivisionAsync(
            club.Id, new UpdateTeamDivisionRequest { Division = null }, SeedIds.AdminUser, Ct);

        Assert.Null(dto.Division);
        Assert.Null(db.Teams.Single(t => t.Id == club.Id).Division);
    }

    [Fact]
    public async Task Moving_a_club_to_the_same_division_writes_nothing()
    {
        using var db = CreateContext();
        var club = db.Teams.First(t => t.Division == Competition.LeagueOne);
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        await service.UpdateDivisionAsync(
            club.Id, new UpdateTeamDivisionRequest { Division = Competition.LeagueOne },
            SeedIds.AdminUser, Ct);

        Assert.Empty(db.AuditLogs);
    }

    [Theory]
    [InlineData(Competition.FACup)]
    [InlineData(Competition.FifaWorldCup)]
    public async Task Competitions_that_are_not_divisions_are_rejected(Competition competition)
    {
        using var db = CreateContext();
        var club = db.Teams.First(t => t.Division == Competition.LeagueOne);
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateDivisionAsync(
                club.Id, new UpdateTeamDivisionRequest { Division = competition }, SeedIds.AdminUser, Ct));

        Assert.Equal("teams.invalidDivision", ex.Key);
    }

    [Fact]
    public async Task A_national_team_cannot_be_given_a_division()
    {
        using var db = CreateContext();
        var brazil = db.Teams.Single(t => t.Name == "Brazil");
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateDivisionAsync(
                brazil.Id, new UpdateTeamDivisionRequest { Division = Competition.PremierLeague },
                SeedIds.AdminUser, Ct));

        Assert.Equal("teams.nationalTeamNoDivision", ex.Key);
    }

    [Fact]
    public async Task An_unknown_team_is_not_found()
    {
        using var db = CreateContext();
        var service = CreateService(db, new FakeTeamCatalogProvider(Rosters()));

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateDivisionAsync(
                Guid.NewGuid(), new UpdateTeamDivisionRequest { Division = Competition.LeagueOne },
                SeedIds.AdminUser, Ct));

        Assert.Equal("notFound.team", ex.Key);
    }
}
