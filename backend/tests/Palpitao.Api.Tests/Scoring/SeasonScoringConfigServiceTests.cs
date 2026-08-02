using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

public class SeasonScoringConfigServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("44444444-4444-4444-4444-444444444401");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext(TournamentType type = TournamentType.PalpitaoEngland)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        db.Seasons.Add(new Season
        {
            Id = SeasonId,
            Name = "Season",
            TournamentType = type,
            StartDate = new DateOnly(2025, 8, 1),
            EndDate = new DateOnly(2026, 5, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db;
    }

    private static SeasonScoringConfigService Build(AppDbContext db, Guid? group = null)
        => new(db, new AuditService(db), new FakeCurrentGroupService(group));

    private static ScoringConfigRequest ToRequest(ScoringConfigDto dto) => new()
    {
        BasePoints = dto.BasePoints,
        Rules = dto.Rules,
        ScoreEntries = dto.ScoreEntries,
        MultiplierRules = dto.MultiplierRules,
        ClassicTeams = dto.Teams
            .Where(t => t.IsClassic)
            .Select(t => new ScoringClassicTeamRequest
            {
                TeamId = t.TeamId,
                Competition = t.ClassicCompetition!.Value,
            })
            .ToList(),
    };

    [Fact]
    public async Task GetConfig_returns_default_england_ruleset()
    {
        using var db = CreateContext();
        var svc = Build(db);

        var dto = await svc.GetConfigAsync(SeasonId, Ct);

        Assert.Equal(1, dto.BasePoints.ColumnOnly);
        Assert.Equal(3, dto.BasePoints.Traditional);
        Assert.Equal(10, dto.BasePoints.ExtraUncommon);
        // League One defaults to x2 (normal and classic).
        var leagueOne = dto.MultiplierRules.Single(r => r.Competition == Competition.LeagueOne);
        Assert.Equal(2, leagueOne.Multiplier);
        // Two classic groups are pre-selected: the Big Seven and the Championship pair.
        Assert.Equal(9, dto.Teams.Count(t => t.IsClassic));
        Assert.Equal(7, dto.Teams.Count(t => t.ClassicCompetition == Competition.PremierLeague));
        var championship = dto.Teams.Where(t => t.ClassicCompetition == Competition.Championship).ToList();
        Assert.Equal(["Millwall", "West Ham United"], championship.Select(t => t.Name).Order());
        // ...and a Championship derby doubles.
        var champRegular = dto.MultiplierRules
            .Single(r => r.Competition == Competition.Championship && r.Phase == MatchPhase.Regular);
        Assert.Equal(1, champRegular.Multiplier);
        Assert.Equal(2, champRegular.ClassicMultiplier);
    }

    [Fact]
    public async Task Update_round_trips_the_classic_groups()
    {
        using var db = CreateContext();
        var svc = Build(db);

        await svc.UpdateAsync(SeasonId, ToRequest(await svc.GetConfigAsync(SeasonId, Ct)), Admin, Ct);
        var ruleSet = await svc.GetRuleSetAsync(SeasonId, Ct);

        var millwall = await db.Teams.FirstAsync(t => t.Name == "Millwall", Ct);
        var westHam = await db.Teams.FirstAsync(t => t.Name == "West Ham United", Ct);
        Assert.True(ruleSet.IsClassicPair(millwall.Id, westHam.Id));
        // Across groups it is not a classic, so the FA Cup row keeps its normal value.
        Assert.False(ruleSet.IsClassicPair(westHam.Id, SeedIds.Arsenal));
    }

    [Fact]
    public async Task Update_rejects_a_group_the_tournament_does_not_allow()
    {
        using var db = CreateContext();
        var svc = Build(db);
        var request = ToRequest(await svc.GetConfigAsync(SeasonId, Ct));
        request.ClassicTeams[0].Competition = Competition.LeagueOne;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UpdateAsync(SeasonId, request, Admin, Ct));
        Assert.Equal("scoring.invalidClassicCompetition", ex.Key);
    }

    [Fact]
    public async Task Update_rejects_a_team_in_two_groups()
    {
        using var db = CreateContext();
        var svc = Build(db);
        var request = ToRequest(await svc.GetConfigAsync(SeasonId, Ct));
        request.ClassicTeams.Add(new ScoringClassicTeamRequest
        {
            TeamId = request.ClassicTeams[0].TeamId,
            Competition = Competition.Championship,
        });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UpdateAsync(SeasonId, request, Admin, Ct));
        Assert.Equal("scoring.duplicateClassicTeam", ex.Key);
    }

    [Fact]
    public async Task GetRuleSet_uses_defaults_until_a_config_is_saved()
    {
        using var db = CreateContext();
        var svc = Build(db);

        var ruleSet = await svc.GetRuleSetAsync(SeasonId, Ct);
        await svc.GetConfigAsync(SeasonId, Ct);

        Assert.Equal(2, ruleSet.MultiplierFor(Competition.LeagueOne, MatchPhase.Regular, false));
        Assert.Equal(ScoreCategory.Uncommon, ruleSet.CategoryForExactScore(3, 2));
        // Neither a pure read nor a config GET creates a persisted row.
        Assert.False(await db.SeasonScoringConfigs.AnyAsync(Ct));
    }

    [Fact]
    public async Task Update_then_GetRuleSet_reflects_custom_multiplier()
    {
        using var db = CreateContext();
        var svc = Build(db);

        var dto = await svc.GetConfigAsync(SeasonId, Ct);
        var request = ToRequest(dto);
        request.MultiplierRules.Single(r => r.Competition == Competition.LeagueOne).Multiplier = 3;

        await svc.UpdateAsync(SeasonId, request, Admin, Ct);
        var ruleSet = await svc.GetRuleSetAsync(SeasonId, Ct);

        Assert.Equal(3, ruleSet.MultiplierFor(Competition.LeagueOne, MatchPhase.Regular, false));
    }

    [Fact]
    public async Task Update_then_GetRuleSet_reflects_custom_category()
    {
        using var db = CreateContext();
        var svc = Build(db);

        var dto = await svc.GetConfigAsync(SeasonId, Ct);
        var request = ToRequest(dto);
        // Reclassify 3x2 from Uncommon to Traditional.
        request.ScoreEntries.Single(e => e.Low == 2 && e.High == 3).Category = ScoreCategory.Traditional;

        await svc.UpdateAsync(SeasonId, request, Admin, Ct);
        var ruleSet = await svc.GetRuleSetAsync(SeasonId, Ct);

        Assert.Equal(ScoreCategory.Traditional, ruleSet.CategoryForExactScore(3, 2));
    }

    [Fact]
    public async Task Update_rejects_multiplier_below_one()
    {
        using var db = CreateContext();
        var svc = Build(db);
        var request = ToRequest(await svc.GetConfigAsync(SeasonId, Ct));
        request.MultiplierRules[0].Multiplier = 0;

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.UpdateAsync(SeasonId, request, Admin, Ct));
    }

    [Fact]
    public async Task Update_rejects_negative_base_points()
    {
        using var db = CreateContext();
        var svc = Build(db);
        var request = ToRequest(await svc.GetConfigAsync(SeasonId, Ct));
        request.BasePoints.Traditional = -1;

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.UpdateAsync(SeasonId, request, Admin, Ct));
    }

    [Fact]
    public async Task GetConfig_returns_the_classic_special_rules_by_default()
    {
        using var db = CreateContext();
        var dto = await Build(db).GetConfigAsync(SeasonId, Ct);

        Assert.Equal(16, dto.Rules.FlavioFromRound);
        Assert.Equal(1, dto.Rules.AbsenceFromRound);
        Assert.Equal(20, dto.Rules.AbsencePenaltyPoints);
        Assert.Equal(5, dto.Rules.AbsenceEliminationCount);
    }

    [Fact]
    public async Task GetRuleParams_falls_back_to_defaults_without_a_config_row()
    {
        using var db = CreateContext();

        var rules = await Build(db).GetRuleParamsAsync(SeasonId, Ct);

        Assert.Equal(SeasonRuleParams.Defaults, rules);
    }

    [Fact]
    public async Task Update_round_trips_the_special_rules()
    {
        using var db = CreateContext();
        var svc = Build(db);
        var request = ToRequest(await svc.GetConfigAsync(SeasonId, Ct));
        request.Rules = new ScoringRulesDto
        {
            FlavioFromRound = 10,
            AbsenceFromRound = 3,
            AbsencePenaltyPoints = 0, // legitimate: no points penalty at all
            AbsenceEliminationCount = 4,
        };

        await svc.UpdateAsync(SeasonId, request, Admin, Ct);

        var saved = await svc.GetConfigAsync(SeasonId, Ct);
        Assert.Equal(10, saved.Rules.FlavioFromRound);
        Assert.Equal(3, saved.Rules.AbsenceFromRound);
        Assert.Equal(0, saved.Rules.AbsencePenaltyPoints);
        Assert.Equal(4, saved.Rules.AbsenceEliminationCount);

        // And the scalar read path sees the same values.
        Assert.Equal(new SeasonRuleParams(10, 3, 0, 4), await svc.GetRuleParamsAsync(SeasonId, Ct));
    }

    [Theory]
    [InlineData(0, 1, 20, 5, "scoring.flavioFromRoundMin")]
    [InlineData(16, 0, 20, 5, "scoring.absenceFromRoundMin")]
    [InlineData(16, 1, -1, 5, "scoring.absencePenaltyNegative")]
    [InlineData(16, 1, 20, 0, "scoring.absenceEliminationMin")]
    public async Task Update_rejects_out_of_range_special_rules(
        int flavioFrom, int absenceFrom, int penalty, int elimination, string expectedKey)
    {
        using var db = CreateContext();
        var svc = Build(db);
        var request = ToRequest(await svc.GetConfigAsync(SeasonId, Ct));
        request.Rules = new ScoringRulesDto
        {
            FlavioFromRound = flavioFrom,
            AbsenceFromRound = absenceFrom,
            AbsencePenaltyPoints = penalty,
            AbsenceEliminationCount = elimination,
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UpdateAsync(SeasonId, request, Admin, Ct));
        Assert.Equal(expectedKey, ex.Key);
    }

    [Fact]
    public async Task World_cup_season_seeds_phase_multipliers_and_champion_classics()
    {
        using var db = CreateContext(TournamentType.FifaWorldCup);
        var svc = Build(db);

        var dto = await svc.GetConfigAsync(SeasonId, Ct);
        var ruleSet = await svc.GetRuleSetAsync(SeasonId, Ct);

        // Round of 32 classic doubles the phase multiplier (x2 -> x4).
        Assert.Equal(4, ruleSet.MultiplierFor(Competition.FifaWorldCup, MatchPhase.WorldCupRoundOf32, true));
        // Group-stage classics are not doubled.
        Assert.Equal(1, ruleSet.MultiplierFor(Competition.FifaWorldCup, MatchPhase.WorldCupGroupStage, true));
        // The seven world champions are pre-selected, all in the World Cup group; candidates
        // are national teams.
        var classics = dto.Teams.Where(t => t.IsClassic).ToList();
        Assert.Equal(7, classics.Count);
        Assert.All(classics, t => Assert.Equal(Competition.FifaWorldCup, t.ClassicCompetition));
    }

    [Fact]
    public async Task GetConfig_is_denied_for_a_season_in_another_group()
    {
        using var db = CreateContext();
        // Season belongs to the default group; act as a different group.
        var svc = Build(db, group: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetConfigAsync(SeasonId, Ct));
    }
}
