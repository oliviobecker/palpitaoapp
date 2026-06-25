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
        ScoreEntries = dto.ScoreEntries,
        MultiplierRules = dto.MultiplierRules,
        ClassicTeamIds = dto.Teams.Where(t => t.IsClassic).Select(t => t.TeamId).ToList(),
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
        // The seven Big Seven clubs are pre-selected as classics.
        Assert.Equal(7, dto.Teams.Count(t => t.IsClassic));
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
        // The seven world champions are pre-selected; candidates are national teams.
        Assert.Equal(7, dto.Teams.Count(t => t.IsClassic));
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
