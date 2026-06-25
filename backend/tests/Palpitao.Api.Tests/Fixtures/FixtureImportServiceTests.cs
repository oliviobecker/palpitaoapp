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

public class FixtureImportServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    // --- Fake provider -----------------------------------------------------
    private sealed class FakeFixtureProvider : IFixtureProvider
    {
        public List<FixtureCandidateDto> Fixtures { get; set; } = new();
        public Exception? ThrowOnSearch { get; set; }
        public string SourceName => "FakeSource";

        public Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
            DateTime startDate, DateTime endDate,
            IReadOnlyList<Competition> competitions, CancellationToken cancellationToken)
        {
            if (ThrowOnSearch is not null)
            {
                throw ThrowOnSearch;
            }

            IReadOnlyList<FixtureCandidateDto> result = Fixtures
                .Where(f => competitions.Contains(f.Competition))
                .ToList();
            return Task.FromResult(result);
        }
    }

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

    private static FixtureImportService CreateService(
        AppDbContext db, FakeFixtureProvider provider, bool enabled = true)
    {
        var options = Options.Create(new FixtureOptions { EnableExternalFixtureImport = enabled });
        var audit = new AuditService(db);
        var current = new FakeCurrentGroupService();
        var scoringConfig = new SeasonScoringConfigService(db, audit, current);
        return new FixtureImportService(db, provider, new ScoringService(), scoringConfig, audit, current, options);
    }

    private static Guid CreateDraftRound(AppDbContext db, int number = 1)
    {
        var id = Guid.NewGuid();
        db.Rounds.Add(new Round
        {
            Id = id,
            SeasonId = SeasonId,
            Number = number,
            Status = RoundStatus.Draft,
            CreatedByUserId = Admin,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    private static FixtureCandidateDto Fixture(
        string home, string away, DateTime startsAt,
        Competition competition = Competition.PremierLeague,
        MatchPhase phase = MatchPhase.Regular,
        string externalId = "ext-1")
        => new()
        {
            ExternalId = externalId,
            Competition = competition,
            Phase = phase,
            HomeTeamName = home,
            AwayTeamName = away,
            StartsAt = startsAt,
            Source = "FakeSource",
        };

    private static ImportFixtureItem ToImport(FixtureCandidateDto c) => new()
    {
        ExternalId = c.ExternalId,
        Competition = c.Competition,
        Phase = c.Phase,
        HomeTeamName = c.HomeTeamName,
        AwayTeamName = c.AwayTeamName,
        StartsAt = c.StartsAt,
        Source = c.Source,
    };

    // -----------------------------------------------------------------------
    // Search
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_rejects_end_before_start()
    {
        using var db = CreateContext();
        var service = CreateService(db, new FakeFixtureProvider());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SearchAsync(
            new SearchFixturesRequest
            {
                StartDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            }, Admin, Ct));

        Assert.Equal("fixtures.endBeforeStart", ex.Key);
    }

    [Fact]
    public async Task Search_returns_normalized_candidates_with_multiplier()
    {
        using var db = CreateContext();
        var provider = new FakeFixtureProvider
        {
            Fixtures =
            {
                Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc)),
                Fixture("Luton", "Reading", new DateTime(2026, 8, 16, 15, 0, 0, DateTimeKind.Utc), Competition.LeagueOne),
            },
        };
        var service = CreateService(db, provider);

        var response = await service.SearchAsync(new SearchFixturesRequest
        {
            StartDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
        }, Admin, Ct);

        Assert.Equal("FakeSource", response.Source);
        Assert.Equal(2, response.Fixtures.Count);

        var derby = response.Fixtures.Single(f => f.HomeTeamName == "Arsenal");
        Assert.True(derby.IsBigSevenMatch);
        Assert.Equal(2, derby.SuggestedMultiplier); // PL Big Seven derby

        var leagueOne = response.Fixtures.Single(f => f.Competition == Competition.LeagueOne);
        Assert.Equal(2, leagueOne.SuggestedMultiplier); // League One always x2
        Assert.False(leagueOne.IsBigSevenMatch);
    }

    [Fact]
    public async Task Search_normal_match_suggests_multiplier_one()
    {
        using var db = CreateContext();
        var provider = new FakeFixtureProvider
        {
            Fixtures = { Fixture("Arsenal", "Brentford", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc)) },
        };
        var service = CreateService(db, provider);

        var response = await service.SearchAsync(new SearchFixturesRequest
        {
            StartDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
        }, Admin, Ct);

        Assert.Equal(1, response.Fixtures.Single().SuggestedMultiplier);
    }

    [Fact]
    public async Task Search_disabled_throws()
    {
        using var db = CreateContext();
        var service = CreateService(db, new FakeFixtureProvider(), enabled: false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SearchAsync(
            new SearchFixturesRequest
            {
                StartDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            }, Admin, Ct));

        Assert.Equal("fixtures.importDisabled", ex.Key);
    }

    [Fact]
    public async Task Search_flags_fixtures_already_in_round()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var provider = new FakeFixtureProvider
        {
            Fixtures = { Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc)) },
        };
        var service = CreateService(db, provider);

        // Import once.
        await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures = provider.Fixtures.Select(ToImport).ToList(),
        }, Admin, Ct);

        var response = await service.SearchAsync(new SearchFixturesRequest
        {
            StartDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
            RoundId = roundId,
        }, Admin, Ct);

        Assert.True(response.Fixtures.Single().IsAlreadyAddedToRound);
    }

    [Fact]
    public async Task Search_failure_is_audited_and_rethrown()
    {
        using var db = CreateContext();
        var provider = new FakeFixtureProvider { ThrowOnSearch = new BusinessRuleException("fixtures.fetchFailed") };
        var service = CreateService(db, provider);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SearchAsync(new SearchFixturesRequest
        {
            StartDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc),
        }, Admin, Ct));

        Assert.Contains(db.AuditLogs, a => a.Action == "FixtureSearchFailed");
    }

    // -----------------------------------------------------------------------
    // Import
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Import_creates_round_matches()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        var result = await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures =
            {
                ToImport(Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc))),
            },
        }, Admin, Ct);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, await db.RoundMatches.CountAsync(m => m.RoundId == roundId));
    }

    [Fact]
    public async Task Import_creates_missing_teams_with_big_seven_flag()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        var result = await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures =
            {
                // Arsenal exists (seed, Big Seven); Crewe Alexandra is outside the
                // seeded league rosters, so it is created and is not Big Seven.
                ToImport(Fixture("Arsenal", "Crewe Alexandra", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc))),
            },
        }, Admin, Ct);

        Assert.Equal(1, result.CreatedTeamCount);
        var crewe = await db.Teams.SingleAsync(t => t.Name == "Crewe Alexandra");
        Assert.False(crewe.IsBigSevenClub);
    }

    [Fact]
    public async Task Import_creates_big_seven_team_when_new()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        // Remove the seeded Newcastle to force creation.
        db.Teams.Remove(await db.Teams.SingleAsync(t => t.Name == "Newcastle"));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeFixtureProvider());

        await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures =
            {
                ToImport(Fixture("Newcastle United", "Burnley", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc))),
            },
        }, Admin, Ct);

        var newcastle = await db.Teams.SingleAsync(t => t.Name == "Newcastle United");
        Assert.True(newcastle.IsBigSevenClub);
    }

    [Fact]
    public async Task Import_skips_duplicates_already_in_round()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());
        var fixture = ToImport(Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc)));

        await service.ImportAsync(roundId, new ImportFixturesRequest { Fixtures = { fixture } }, Admin, Ct);
        var second = await service.ImportAsync(roundId, new ImportFixturesRequest { Fixtures = { fixture } }, Admin, Ct);

        Assert.Equal(0, second.ImportedCount);
        Assert.Equal(1, second.SkippedDuplicateCount);
        Assert.Equal(1, await db.RoundMatches.CountAsync(m => m.RoundId == roundId));
    }

    [Fact]
    public async Task Import_deduplicates_within_the_same_batch()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());
        var fixture = ToImport(Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc)));

        var result = await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures = { fixture, fixture },
        }, Admin, Ct);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task Import_requires_at_least_one_fixture()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(
            roundId, new ImportFixturesRequest(), Admin, Ct));

        Assert.Equal("fixtures.selectNone", ex.Key);
    }

    [Fact]
    public async Task Import_blocks_second_league_one_without_justification()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(
            roundId, new ImportFixturesRequest
            {
                Fixtures =
                {
                    ToImport(Fixture("Luton", "Reading", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc), Competition.LeagueOne, externalId: "a")),
                    ToImport(Fixture("Wigan", "Bolton", new DateTime(2026, 8, 16, 15, 0, 0, DateTimeKind.Utc), Competition.LeagueOne, externalId: "b")),
                },
            }, Admin, Ct));

        Assert.Equal("fixtures.leagueOneSingle", ex.Key);
    }

    [Fact]
    public async Task Import_allows_two_league_one_with_justification()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        var result = await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            LeagueOneJustification = "Rodada especial com dois jogos da League One.",
            Fixtures =
            {
                ToImport(Fixture("Luton", "Reading", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc), Competition.LeagueOne, externalId: "a")),
                ToImport(Fixture("Wigan", "Bolton", new DateTime(2026, 8, 16, 15, 0, 0, DateTimeKind.Utc), Competition.LeagueOne, externalId: "b")),
            },
        }, Admin, Ct);

        Assert.Equal(2, result.ImportedCount);
    }

    [Fact]
    public async Task Import_recomputes_first_match_when_round_published()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        // Seed one match and publish the round.
        await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures = { ToImport(Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 20, 16, 0, 0, DateTimeKind.Utc))) },
        }, Admin, Ct);
        var round = await db.Rounds.Include(r => r.Matches).SingleAsync(r => r.Id == roundId);
        round.Status = RoundStatus.Published;
        round.FirstMatchStartsAt = round.Matches.Min(m => m.StartsAt);
        await db.SaveChangesAsync();

        var earlier = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures = { ToImport(Fixture("Liverpool", "Newcastle", earlier, externalId: "c")) },
        }, Admin, Ct);

        var updated = await db.Rounds.SingleAsync(r => r.Id == roundId);
        Assert.Equal(earlier, updated.FirstMatchStartsAt);
    }

    [Fact]
    public async Task Import_audits_the_operation()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var service = CreateService(db, new FakeFixtureProvider());

        await service.ImportAsync(roundId, new ImportFixturesRequest
        {
            Fixtures = { ToImport(Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc))) },
        }, Admin, Ct);

        Assert.Contains(db.AuditLogs, a => a.Action == "FixturesImported");
    }

    [Fact]
    public async Task Import_blocked_on_closed_round()
    {
        using var db = CreateContext();
        var roundId = CreateDraftRound(db);
        var round = await db.Rounds.SingleAsync(r => r.Id == roundId);
        round.Status = RoundStatus.Locked;
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeFixtureProvider());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(
            roundId, new ImportFixturesRequest
            {
                Fixtures = { ToImport(Fixture("Arsenal", "Chelsea", new DateTime(2026, 8, 15, 13, 30, 0, DateTimeKind.Utc))) },
            }, Admin, Ct));

        Assert.Equal("round.cannotEditClosed", ex.Key);
    }
}
