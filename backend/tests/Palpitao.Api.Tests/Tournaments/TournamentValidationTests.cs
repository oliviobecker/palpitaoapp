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
using Palpitao.Api.Services.Tournaments;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Tournaments;

public class TournamentValidationTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid WorldCupGroup = Guid.Parse("77777777-7777-7777-7777-777777777701");
    private static readonly Guid WorldCupSeason = Guid.Parse("77777777-7777-7777-7777-777777777702");
    private static readonly Guid EnglandSeason = SeedIds.DefaultGroup; // season id == default (England) group id in seeds

    // --- Pure rules ---------------------------------------------------------

    [Fact]
    public void England_allows_only_english_competitions()
    {
        Assert.True(TournamentRules.IsCompetitionAllowed(TournamentType.PalpitaoEngland, Competition.PremierLeague));
        Assert.False(TournamentRules.IsCompetitionAllowed(TournamentType.PalpitaoEngland, Competition.FifaWorldCup));
    }

    [Fact]
    public void World_cup_allows_only_the_world_cup_competition_and_phases()
    {
        Assert.True(TournamentRules.IsCompetitionAllowed(TournamentType.FifaWorldCup, Competition.FifaWorldCup));
        Assert.False(TournamentRules.IsCompetitionAllowed(TournamentType.FifaWorldCup, Competition.PremierLeague));
        Assert.True(TournamentRules.IsPhaseAllowed(TournamentType.FifaWorldCup, MatchPhase.WorldCupQuarterFinal));
        Assert.False(TournamentRules.IsPhaseAllowed(TournamentType.FifaWorldCup, MatchPhase.Regular));
        Assert.False(TournamentRules.IsPhaseAllowed(TournamentType.PalpitaoEngland, MatchPhase.WorldCupFinal));
    }

    [Theory]
    [InlineData(MatchPhase.WorldCupGroupStage, false)]
    [InlineData(MatchPhase.WorldCupRoundOf32, true)]
    [InlineData(MatchPhase.WorldCupRoundOf16, true)]
    [InlineData(MatchPhase.WorldCupQuarterFinal, true)]
    [InlineData(MatchPhase.WorldCupFinal, true)]
    public void World_cup_knockout_classification(MatchPhase phase, bool isKnockout)
        => Assert.Equal(isKnockout, TournamentRules.IsWorldCupKnockout(phase));

    [Theory]
    [InlineData(MatchPhase.WorldCupGroupStage, false)]
    [InlineData(MatchPhase.WorldCupRoundOf16, false)]
    [InlineData(MatchPhase.WorldCupQuarterFinal, true)]
    [InlineData(MatchPhase.WorldCupSemiFinal, true)]
    [InlineData(MatchPhase.WorldCupThirdPlace, true)]
    [InlineData(MatchPhase.WorldCupFinal, true)]
    public void World_cup_flavio_phase_classification(MatchPhase phase, bool isFlavio)
        => Assert.Equal(isFlavio, TournamentRules.IsWorldCupFlavioPhase(phase));

    // --- Enforced in RoundService ------------------------------------------

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        // A World Cup group + season (England default group/season come from the seed).
        db.Groups.Add(new Group
        {
            Id = WorldCupGroup,
            Name = "Copa 2026",
            Slug = "copa-2026",
            CreatedByUserId = SeedIds.AdminUser,
            OwnerUserId = SeedIds.AdminUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Seasons.Add(new Season { Id = WorldCupSeason, GroupId = WorldCupGroup, Name = "WC", TournamentType = TournamentType.FifaWorldCup, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Seasons.Add(new Season { Id = EnglandSeason, GroupId = SeedIds.DefaultGroup, Name = "ENG", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return db;
    }

    private static RoundService Service(AppDbContext db, Guid groupId)
        => new(db, new AuditService(db), new FakeCurrentGroupService(groupId), TestServices.ScoringConfig(db));

    private static Guid TeamId(AppDbContext db, string name) => db.Teams.Single(t => t.Name == name).Id;

    private static CreateMatchRequest Match(Guid home, Guid away, Competition competition, MatchPhase phase)
        => new()
        {
            Competition = competition,
            Phase = phase,
            HomeTeamId = home,
            AwayTeamId = away,
            StartsAt = DateTime.UtcNow.AddDays(3),
        };

    [Fact]
    public async Task World_cup_group_rejects_an_english_competition()
    {
        using var db = CreateContext();
        var service = Service(db, WorldCupGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = WorldCupSeason, Number = 1 }, SeedIds.AdminUser, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddMatchAsync(
            round.Id, Match(TeamId(db, "Arsenal"), TeamId(db, "Chelsea"), Competition.PremierLeague, MatchPhase.Regular), SeedIds.AdminUser, Ct));
        Assert.Equal("tournament.competitionNotAllowed", ex.Key);
    }

    [Fact]
    public async Task England_group_rejects_the_world_cup_competition()
    {
        using var db = CreateContext();
        var service = Service(db, SeedIds.DefaultGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = EnglandSeason, Number = 1 }, SeedIds.AdminUser, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddMatchAsync(
            round.Id, Match(TeamId(db, "Brazil"), TeamId(db, "Germany"), Competition.FifaWorldCup, MatchPhase.WorldCupGroupStage), SeedIds.AdminUser, Ct));
        Assert.Equal("tournament.competitionNotAllowed", ex.Key);
    }

    [Fact]
    public async Task World_cup_group_accepts_a_world_cup_match()
    {
        using var db = CreateContext();
        var service = Service(db, WorldCupGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = WorldCupSeason, Number = 1 }, SeedIds.AdminUser, Ct);

        var match = await service.AddMatchAsync(
            round.Id, Match(TeamId(db, "Brazil"), TeamId(db, "Germany"), Competition.FifaWorldCup, MatchPhase.WorldCupGroupStage), SeedIds.AdminUser, Ct);

        Assert.Equal(Competition.FifaWorldCup, match.Competition);
        Assert.Equal(MatchPhase.WorldCupGroupStage, match.Phase);
    }

    // --- Per-season FA Cup toggle ------------------------------------------

    private static void DisableFaCup(AppDbContext db)
    {
        db.Seasons.Single(s => s.Id == EnglandSeason).FaCupEnabled = false;
        db.SaveChanges();
    }

    private static CreateMatchRequest FaCupMatch(AppDbContext db)
        => Match(TeamId(db, "Arsenal"), TeamId(db, "Chelsea"), Competition.FACup, MatchPhase.Regular);

    [Fact]
    public async Task England_group_accepts_a_fa_cup_match_while_the_season_allows_it()
    {
        using var db = CreateContext();
        var service = Service(db, SeedIds.DefaultGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = EnglandSeason, Number = 1 }, SeedIds.AdminUser, Ct);

        var match = await service.AddMatchAsync(round.Id, FaCupMatch(db), SeedIds.AdminUser, Ct);

        Assert.Equal(Competition.FACup, match.Competition);
    }

    [Fact]
    public async Task England_group_rejects_a_fa_cup_match_when_the_season_disabled_it()
    {
        using var db = CreateContext();
        var service = Service(db, SeedIds.DefaultGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = EnglandSeason, Number = 1 }, SeedIds.AdminUser, Ct);
        DisableFaCup(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AddMatchAsync(round.Id, FaCupMatch(db), SeedIds.AdminUser, Ct));

        Assert.Equal("season.faCupDisabled", ex.Key);
    }

    [Fact]
    public async Task Turning_a_match_into_a_fa_cup_one_is_rejected_when_disabled()
    {
        using var db = CreateContext();
        var service = Service(db, SeedIds.DefaultGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = EnglandSeason, Number = 1 }, SeedIds.AdminUser, Ct);
        var match = await service.AddMatchAsync(
            round.Id, Match(TeamId(db, "Arsenal"), TeamId(db, "Chelsea"), Competition.PremierLeague, MatchPhase.Regular), SeedIds.AdminUser, Ct);
        DisableFaCup(db);

        var request = new UpdateMatchRequest
        {
            Competition = Competition.FACup,
            Phase = MatchPhase.Regular,
            HomeTeamId = match.HomeTeamId,
            AwayTeamId = match.AwayTeamId,
            StartsAt = match.StartsAt,
        };
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateMatchAsync(match.Id, request, SeedIds.AdminUser, Ct));

        Assert.Equal("season.faCupDisabled", ex.Key);
    }

    [Fact]
    public async Task A_fa_cup_match_already_in_the_round_stays_editable_after_the_toggle_goes_off()
    {
        using var db = CreateContext();
        var service = Service(db, SeedIds.DefaultGroup);
        var round = await service.CreateAsync(new CreateRoundRequest { SeasonId = EnglandSeason, Number = 1 }, SeedIds.AdminUser, Ct);
        var match = await service.AddMatchAsync(round.Id, FaCupMatch(db), SeedIds.AdminUser, Ct);
        DisableFaCup(db);

        // Turning the competition off must not freeze the matches already in the round:
        // a wrong kickoff still has to be fixable.
        var newKickoff = match.StartsAt.AddHours(2);
        var updated = await service.UpdateMatchAsync(match.Id, new UpdateMatchRequest
        {
            Competition = Competition.FACup,
            Phase = MatchPhase.Regular,
            HomeTeamId = match.HomeTeamId,
            AwayTeamId = match.AwayTeamId,
            StartsAt = newKickoff,
        }, SeedIds.AdminUser, Ct);

        Assert.Equal(newKickoff, updated.StartsAt);
    }
}
