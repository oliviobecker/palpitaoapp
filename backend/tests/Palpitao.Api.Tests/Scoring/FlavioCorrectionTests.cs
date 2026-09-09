using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.AdminPredictions;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Ocr;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Tests.TestSupport;
using Palpitao.Api.Validation;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

public partial class RoundScoringServiceTests
{
    private static FlavioOverrideService Overrides(AppDbContext db, Guid? groupId = null)
        => new(db, new FakeCurrentGroupService(groupId), new AuditService(db), new FlavioRuleService(db),
            TestServices.ScoringConfig(db), Build(db).Scoring);

    private static void ConfigureFlavioFromFive(AppDbContext db)
    {
        db.SeasonScoringConfigs.Add(new SeasonScoringConfig
        {
            Id = Guid.NewGuid(), GroupId = SeedIds.DefaultGroup, SeasonId = SeasonId,
            FlavioFromRound = 5, ColumnOnlyPoints = 1, TraditionalPoints = 3,
            MediumPoints = 5, UncommonPoints = 7, ExtraUncommonPoints = 10,
            ScoreEntries = ScoringDefaults.ScoreCategories().Select(c => new ScoringScoreEntry
                { Id = Guid.NewGuid(), Low = c.Low, High = c.High, Category = c.Category }).ToList(),
            MultiplierRules = ScoringDefaults.MultiplierRules(TournamentType.PalpitaoEngland)
                .Select(m => new ScoringMultiplierRule { Id = Guid.NewGuid(), Competition = m.Competition,
                    Phase = m.Phase, Multiplier = m.Normal, ClassicMultiplier = m.Classic }).ToList(),
        });
        db.SaveChanges();
    }

    private static async Task EnterLateExternal(
        AppDbContext db, RoundDto round, Guid userId, PredictionSource source, int home, int away)
    {
        var storedRound = await db.Rounds.FindAsync(round.Id);
        storedRound!.PublishedAt = DateTime.UtcNow.AddDays(-3);
        storedRound.MirrorPublishedAt = storedRound.PublishedAt;
        await db.SaveChangesAsync();
        var current = new FakeCurrentGroupService();
        var audit = new AuditService(db);
        if (source == PredictionSource.AdminManual)
        {
            await new AdminPredictionService(db, audit, current, TestServices.Absences(db))
                .SaveManualAsync(round.Id, new ManualPredictionRequest
                {
                    UserId = userId, AllowAfterDeadline = true, Justification = "Enviado pelo WhatsApp.",
                    Predictions = [new PredictionItemRequest { RoundMatchId = round.Matches[0].Id,
                        PredictedHomeScore = home, PredictedAwayScore = away }],
                }, Admin, Ct);
        }
        else
        {
            var batch = new OcrImportBatch
            {
                Id = Guid.NewGuid(), RoundId = round.Id, UploadedByUserId = Admin,
                Status = OcrBatchStatus.Reviewed,
                Candidates = [new OcrPredictionCandidate { Id = Guid.NewGuid(), RoundId = round.Id,
                    UserId = userId, RoundMatchId = round.Matches[0].Id,
                    PredictedHomeScore = home, PredictedAwayScore = away }],
            };
            db.OcrImportBatches.Add(batch);
            await db.SaveChangesAsync();
            await new PredictionImportService(db, audit, current, new OcrAliasService(db, audit, current))
                .ConfirmAsync(batch.Id, Admin, Ct);
        }
    }

    private static async Task<(Kit Kit, Guid Vilaca, Guid Bruno, RoundDto Five, RoundDto Six)> History(
        AppDbContext db, PredictionSource source = PredictionSource.AdminManual)
    {
        ConfigureFlavioFromFive(db);
        var kit = Build(db);
        var vilaca = CreateParticipant(db, "Vilaça");
        var bruno = CreateParticipant(db, "Bruno");
        var one = await PublishedRound(kit, 1);
        await SavePredictions(kit, one, vilaca, (2, 1));
        await SavePredictions(kit, one, bruno, (1, 0));
        await kit.Rounds.LockAsync(one.Id, Admin, Ct);
        await SetResults(kit, one, (2, 1));
        await kit.Scoring.ScoreRoundAsync(one.Id, Admin, Ct); // 3 vs 1
        var five = await PublishedRound(kit, 5);
        await EnterLateExternal(db, five, vilaca, source, 0, 0);
        await EnterLateExternal(db, five, bruno, source, 0, 0);
        await kit.Rounds.LockAsync(five.Id, Admin, Ct);
        await SetResults(kit, five, (0, 0));
        await kit.Scoring.ScoreRoundAsync(five.Id, Admin, Ct); // 5 vs 6 after halving Vilaça
        var six = await PublishedRound(kit, 6);
        await EnterLateExternal(db, six, vilaca, source, 2, 1);
        await EnterLateExternal(db, six, bruno, source, 2, 1);
        await kit.Rounds.LockAsync(six.Id, Admin, Ct);
        await SetResults(kit, six, (2, 1));
        await kit.Scoring.ScoreRoundAsync(six.Id, Admin, Ct); // Bruno halved this time
        return (kit, vilaca, bruno, five, six);
    }

    private static FlavioOverrideRequest Exempt(Guid user, bool isExempt = true) => new()
        { UserId = user, IsExempt = isExempt, Justification = "Palpites enviados no prazo pelo WhatsApp." };

    [Theory]
    [InlineData(PredictionSource.AdminManual)]
    [InlineData(PredictionSource.AdminOcr)]
    public async Task External_entry_can_be_exempted_and_restored_with_historical_cascade(PredictionSource source)
    {
        using var db = CreateContext();
        var h = await History(db, source);
        var service = Overrides(db);
        var before = await service.GetAsync(h.Five.Id, Ct);
        Assert.True(before.Applies); // custom activation in round 5
        var vilacaBefore = before.Participants.Single(p => p.UserId == h.Vilaca);
        Assert.True(vilacaBefore.IsTarget);
        Assert.True(vilacaBefore.SubmittedAt > before.DeadlineUtc);
        Assert.Equal(5, vilacaBefore.GrossPoints);
        Assert.Equal(2, vilacaBefore.FinalPoints);
        var predictionsBefore = await db.Predictions.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.SubmittedAt, p.UpdatedAt, p.Source, p.PredictedHomeScore, p.PredictedAwayScore }).ToListAsync();

        await service.SaveAsync(h.Five.Id, Exempt(h.Vilaca), Admin, Ct);
        var five = await h.Kit.Scoring.GetRoundResultsAsync(h.Five.Id, Ct);
        Assert.False(five.Participants.Single(p => p.UserId == h.Vilaca).FlavioRuleApplied);
        Assert.Equal(5, five.Participants.Single(p => p.UserId == h.Vilaca).FinalPoints);
        var six = await h.Kit.Scoring.GetRoundResultsAsync(h.Six.Id, Ct);
        Assert.True(six.Participants.Single(p => p.UserId == h.Vilaca).FlavioRuleApplied);
        Assert.False(six.Participants.Single(p => p.UserId == h.Bruno).FlavioRuleApplied);
        Assert.Equal(new[] { "Vilaça" }, (await h.Kit.Rounds.GetByIdAsync(h.Six.Id, Ct)).Flavio!.LeaderNames);
        var standings = await h.Kit.Standings.GetStandingsAsync(SeasonId, Ct);
        Assert.All(standings, s => Assert.Equal(9, s.TotalPoints));
        await h.Kit.Scoring.ScoreRoundAsync(h.Five.Id, Admin, Ct); // existing button must also replay chronologically
        await h.Kit.Scoring.RecalculateSeasonAsync(SeasonId, Admin, Ct);
        Assert.Equal(standings.Select(s => (s.UserId, s.TotalPoints)),
            (await h.Kit.Standings.GetStandingsAsync(SeasonId, Ct)).Select(s => (s.UserId, s.TotalPoints)));
        Assert.Equal(predictionsBefore, await db.Predictions.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.SubmittedAt, p.UpdatedAt, p.Source, p.PredictedHomeScore, p.PredictedAwayScore }).ToListAsync());

        var createdAt = (await db.FlavioOverrides.SingleAsync()).CreatedAt;
        await service.SaveAsync(h.Five.Id, Exempt(h.Vilaca, false), Admin, Ct);
        Assert.Equal(createdAt, (await db.FlavioOverrides.SingleAsync()).CreatedAt);
        Assert.True((await h.Kit.Scoring.GetRoundResultsAsync(h.Five.Id, Ct)).Participants.Single(p => p.UserId == h.Vilaca).FlavioRuleApplied);
        Assert.True((await h.Kit.Scoring.GetRoundResultsAsync(h.Six.Id, Ct)).Participants.Single(p => p.UserId == h.Bruno).FlavioRuleApplied);
        var logs = await db.AuditLogs.Where(a => a.Action == "FlavioOverrideChanged").OrderBy(a => a.CreatedAt).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => { Assert.Equal(Admin, log.UserId); Assert.Equal(SeedIds.DefaultGroup, log.GroupId); });
        Assert.Contains("\"IsExempt\":true", logs[1].Details);
        Assert.Contains("\"IsExempt\":false", logs[1].Details);
    }

    [Theory]
    [InlineData(RoundStatus.Published)]
    [InlineData(RoundStatus.Locked)]
    public async Task Unscored_override_does_not_score_or_excuse_an_absence(RoundStatus status)
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(kit, 1);
        if (status == RoundStatus.Locked) await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await Overrides(db).SaveAsync(round.Id, Exempt(user), Admin, Ct);
        Assert.Equal(status, (await db.Rounds.FindAsync(round.Id))!.Status);
        Assert.Empty(await db.RoundParticipantResults.ToListAsync());
        if (status == RoundStatus.Published) await kit.Rounds.LockAsync(round.Id, Admin, Ct);
        await SetResults(kit, round, (2, 1));
        var result = (await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct)).Participants.Single();
        Assert.True(result.WasAbsent);
        Assert.Equal(0, result.FinalPoints);
        Assert.False(result.FlavioRuleApplied);
    }

    [Fact]
    public async Task Failed_recalculation_rolls_back_exemption_audit_scores_and_standings()
    {
        using var db = CreateContext();
        var h = await History(db);
        // Failure occurs only after earlier rounds have been cleared/re-scored.
        (await db.RoundMatches.SingleAsync(m => m.RoundId == h.Six.Id)).HomeScore = null;
        await db.SaveChangesAsync();
        var resultIds = await db.RoundParticipantResults.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();
        var standingIds = await db.Standings.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
        var auditCount = await db.AuditLogs.CountAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Overrides(db).SaveAsync(h.Five.Id, Exempt(h.Vilaca), Admin, Ct));
        db.ChangeTracker.Clear(); // verify committed DB state, not rolled-back tracked entities
        Assert.Empty(await db.FlavioOverrides.ToListAsync());
        Assert.Equal(auditCount, await db.AuditLogs.CountAsync());
        Assert.Equal(resultIds, await db.RoundParticipantResults.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync());
        Assert.Equal(standingIds, await db.Standings.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reopened_round_blocks_both_recalculation_entrypoints_and_preserves_history(bool unlocked)
    {
        using var db = CreateContext();
        var h = await History(db);
        await h.Kit.Rounds.ReopenAsync(h.Six.Id, Admin, Ct);
        if (unlocked) await h.Kit.Rounds.UnlockAsync(h.Six.Id, Admin, Ct);
        var resultIds = await db.RoundParticipantResults.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => h.Kit.Scoring.ScoreRoundAsync(h.Five.Id, Admin, Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Overrides(db).SaveAsync(h.Five.Id, Exempt(h.Vilaca), Admin, Ct));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.FlavioOverrides.ToListAsync());
        Assert.Equal(resultIds, await db.RoundParticipantResults.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync());
        // Completing the reopened round makes the season recalculable again.
        if (unlocked) await h.Kit.Rounds.LockAsync(h.Six.Id, Admin, Ct);
        await h.Kit.Scoring.ScoreRoundAsync(h.Six.Id, Admin, Ct);
        await Overrides(db).SaveAsync(h.Five.Id, Exempt(h.Vilaca), Admin, Ct);
        Assert.Equal(5, (await h.Kit.Scoring.GetRoundResultsAsync(h.Five.Id, Ct))
            .Participants.Single(p => p.UserId == h.Vilaca).FinalPoints);
    }

    [Fact]
    public async Task World_cup_keeps_the_publication_target_when_standings_change()
    {
        using var db = CreateContext();
        var h = await History(db);
        (await db.Seasons.FindAsync(SeasonId))!.TournamentType = TournamentType.FifaWorldCup;
        foreach (var id in new[] { h.Five.Id, h.Six.Id })
        {
            var round = await db.Rounds.FindAsync(id);
            round!.FlavioRuleTargetUserId = h.Bruno; // intentionally different from historical England leader
            var match = await db.RoundMatches.SingleAsync(m => m.RoundId == id);
            match.Competition = Competition.FifaWorldCup;
            match.Phase = MatchPhase.WorldCupQuarterFinal;
            match.ManualMultiplierOverride = 1;
        }
        await db.SaveChangesAsync();
        await h.Kit.Scoring.RecalculateSeasonAsync(SeasonId, Admin, Ct);
        var service = Overrides(db);
        var panel = await service.GetAsync(h.Five.Id, Ct);
        Assert.True(panel.Applies);
        Assert.Equal(h.Bruno, panel.Participants.Single(p => p.IsTarget).UserId);
        Assert.Equal(new[] { "Bruno" }, (await h.Kit.Rounds.GetByIdAsync(h.Five.Id, Ct)).Flavio!.LeaderNames);
        Assert.True(panel.Participants.Single(p => p.UserId == h.Bruno).FlavioRuleApplied);
        await service.SaveAsync(h.Five.Id, Exempt(h.Bruno), Admin, Ct);
        Assert.False((await service.GetAsync(h.Five.Id, Ct)).Participants.Single(p => p.UserId == h.Bruno).FlavioRuleApplied);
        Assert.True((await service.GetAsync(h.Six.Id, Ct)).Participants.Single(p => p.UserId == h.Bruno).FlavioRuleApplied);
        Assert.Equal(h.Bruno, (await db.Rounds.FindAsync(h.Five.Id))!.FlavioRuleTargetUserId);
    }

    [Fact]
    public async Task Correction_does_not_read_or_change_another_groups_round_five()
    {
        using var db = CreateContext();
        var h = await History(db);
        var otherGroup = Guid.NewGuid();
        var otherSeason = Guid.NewGuid();
        var otherRound = Guid.NewGuid();
        db.Groups.Add(new Group { Id = otherGroup, Name = "Outro", Slug = "outro", CreatedByUserId = Admin, OwnerUserId = Admin });
        db.Seasons.Add(new Season { Id = otherSeason, GroupId = otherGroup, Name = "Outra temporada" });
        db.Rounds.Add(new Round { Id = otherRound, GroupId = otherGroup, SeasonId = otherSeason,
            Number = 5, Status = RoundStatus.Scored, CreatedByUserId = Admin });
        var otherResult = new RoundParticipantResult { Id = Guid.NewGuid(), GroupId = otherGroup,
            SeasonId = otherSeason, RoundId = otherRound, UserId = h.Vilaca, FinalPoints = 999 };
        db.RoundParticipantResults.Add(otherResult);
        var otherOverride = new FlavioOverride { Id = Guid.NewGuid(), RoundId = otherRound,
            UserId = h.Vilaca, IsExempt = true, Justification = "Outro grupo." };
        db.FlavioOverrides.Add(otherOverride);
        await db.SaveChangesAsync();
        var service = Overrides(db);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(otherRound, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(otherRound, Exempt(h.Vilaca, false), Admin, Ct));
        await service.SaveAsync(h.Five.Id, Exempt(h.Vilaca), Admin, Ct);
        Assert.Equal(999, (await db.RoundParticipantResults.AsNoTracking().SingleAsync(r => r.Id == otherResult.Id)).FinalPoints);
        Assert.True((await db.FlavioOverrides.AsNoTracking().SingleAsync(o => o.Id == otherOverride.Id)).IsExempt);
        Assert.Equal(new[] { "Vilaça" }, (await h.Kit.Rounds.GetByIdAsync(h.Five.Id, Ct)).Flavio!.LeaderNames);
    }

    [Fact]
    public async Task Override_enforces_tenant_participant_status_and_justification()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(kit, 1);
        var service = Overrides(db);
        foreach (var justification in new[] { "", "   ", new string('x', 501) })
        {
            var request = Exempt(user); request.Justification = justification;
            Assert.False(new FlavioOverrideRequestValidator().Validate(request).IsValid);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(round.Id, request, Admin, Ct));
        }
        Assert.True(new FlavioOverrideRequestValidator().Validate(new FlavioOverrideRequest
            { UserId = user, Justification = new string('x', 500) }).IsValid);
        Assert.False(new FlavioOverrideRequestValidator().Validate(Exempt(Guid.Empty)).IsValid);
        await Assert.ThrowsAsync<NotFoundException>(() => Overrides(db, Guid.NewGuid()).GetAsync(round.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => Overrides(db, Guid.NewGuid()).SaveAsync(round.Id, Exempt(user), Admin, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(round.Id, Exempt(Admin), Admin, Ct));
        foreach (var status in new[] { RoundStatus.Draft, RoundStatus.Cancelled })
        {
            (await db.Rounds.FindAsync(round.Id))!.Status = status;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(round.Id, Exempt(user), Admin, Ct));
        }
        Assert.Empty(await db.FlavioOverrides.ToListAsync());
    }
}
