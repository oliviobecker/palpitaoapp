using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.DTOs.Seasons;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.AdminPredictions;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Seasons;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Predictions;

/// <summary>
/// "Participants submit via app vs admin-only" feature: the in-app submission endpoint
/// is gated by <see cref="Group.AllowParticipantsToSubmitPredictions"/>. Admin manual/OCR
/// paths are unaffected and keep their own prediction source.
/// </summary>
public class PredictionSubmissionModeTests
{
    private static readonly Guid SeasonId = Guid.Parse("55555555-5555-5555-5555-555555555501");
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Seasons.Add(new Season
        {
            Id = SeasonId,
            GroupId = SeedIds.DefaultGroup,
            Name = "Season",
            StartDate = new DateOnly(2025, 8, 1),
            EndDate = new DateOnly(2026, 5, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db;
    }

    private static PredictionsService Predictions(AppDbContext db)
        => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static void SetSubmitMode(AppDbContext db, bool allow)
    {
        db.Seasons.First(s => s.Id == SeasonId).AllowParticipantsToSubmitPredictions = allow;
        db.SaveChanges();
    }

    private static Guid AddParticipant(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = $"User {id.ToString()[..4]}",
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

    private static async Task<RoundDto> PublishedRound(AppDbContext db, DateTime firstStartsAt)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService());
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = 1 }, SeedIds.AdminUser, Ct);
        await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
        {
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = firstStartsAt,
        }, SeedIds.AdminUser, Ct);
        return await rounds.PublishAsync(round.Id, SeedIds.AdminUser, Ct);
    }

    private static SavePredictionsRequest Batch(RoundDto round)
        => new()
        {
            Predictions = round.Matches
                .Select(m => new PredictionItemRequest { RoundMatchId = m.Id, PredictedHomeScore = 2, PredictedAwayScore = 1 })
                .ToList(),
        };

    [Fact]
    public void Default_season_allows_in_app_submission()
    {
        using var db = CreateContext();
        Assert.True(db.Seasons.First(s => s.Id == SeasonId).AllowParticipantsToSubmitPredictions);
    }

    [Fact]
    public async Task Participant_can_submit_when_allowed_and_source_is_participant()
    {
        using var db = CreateContext();
        SetSubmitMode(db, true);
        var round = await PublishedRound(db, Future);
        var user = AddParticipant(db);

        await Predictions(db).SavePredictionsAsync(round.Id, user, Batch(round), isEdit: false, Ct);

        Assert.All(db.Predictions.Where(p => p.UserId == user),
            p => Assert.Equal(PredictionSource.Participant, p.Source));
    }

    [Fact]
    public async Task Participant_cannot_submit_when_admin_only()
    {
        using var db = CreateContext();
        SetSubmitMode(db, false);
        var round = await PublishedRound(db, Future);
        var user = AddParticipant(db);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => Predictions(db).SavePredictionsAsync(round.Id, user, Batch(round), isEdit: false, Ct));
        Assert.Equal("prediction.appSubmitDisabled", ex.Key);
        Assert.Empty(db.Predictions.Where(p => p.UserId == user));
    }

    [Fact]
    public async Task Participant_cannot_edit_when_admin_only()
    {
        using var db = CreateContext();
        SetSubmitMode(db, false);
        var round = await PublishedRound(db, Future);
        var user = AddParticipant(db);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => Predictions(db).SavePredictionsAsync(round.Id, user, Batch(round), isEdit: true, Ct));
        Assert.Equal("prediction.appSubmitDisabled", ex.Key);
    }

    [Fact]
    public async Task Closed_round_still_blocks_participant_when_submission_allowed()
    {
        using var db = CreateContext();
        SetSubmitMode(db, true);
        var round = await PublishedRound(db, DateTime.UtcNow.AddMinutes(-5)); // deadline already passed
        var user = AddParticipant(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Predictions(db).SavePredictionsAsync(round.Id, user, Batch(round), isEdit: false, Ct));
        Assert.Equal("prediction.deadlinePassed", ex.Key);
    }

    [Fact]
    public async Task Admin_manual_works_when_admin_only_with_admin_manual_source()
    {
        using var db = CreateContext();
        SetSubmitMode(db, false);
        var round = await PublishedRound(db, Future);
        var user = AddParticipant(db);

        var admin = new AdminPredictionService(db, new AuditService(db), new FakeCurrentGroupService());
        await admin.SaveManualAsync(round.Id, new ManualPredictionRequest
        {
            UserId = user,
            OverwriteExisting = false,
            Predictions = round.Matches
                .Select(m => new PredictionItemRequest { RoundMatchId = m.Id, PredictedHomeScore = 1, PredictedAwayScore = 0 })
                .ToList(),
        }, SeedIds.AdminUser, Ct);

        Assert.All(db.Predictions.Where(p => p.UserId == user),
            p => Assert.Equal(PredictionSource.AdminManual, p.Source));
    }

    [Fact]
    public async Task Season_settings_report_participant_predictions_and_audit_the_change()
    {
        using var db = CreateContext();
        SetSubmitMode(db, true);
        var round = await PublishedRound(db, Future);
        var user = AddParticipant(db);
        await Predictions(db).SavePredictionsAsync(round.Id, user, Batch(round), isEdit: false, Ct);

        var seasons = new SeasonService(db, new AuditService(db),
            new FakeCurrentGroupService(role: GroupRole.GroupAdmin, userId: SeedIds.AdminUser));

        var before = (await seasons.ListAsync(Ct)).First(s => s.Id == SeasonId);
        Assert.True(before.HasParticipantPredictions);
        Assert.True(before.AllowParticipantsToSubmitPredictions);

        var after = await seasons.UpdateAsync(SeasonId, new SeasonRequest
        {
            Name = before.Name,
            StartDate = before.StartDate,
            EndDate = before.EndDate,
            IsActive = before.IsActive,
            AllowParticipantsToViewOthersPredictions = false,
            AllowParticipantsToSubmitPredictions = false,
        }, SeedIds.AdminUser, Ct);

        Assert.False(after.AllowParticipantsToSubmitPredictions);
        // Existing predictions are kept, only new submissions are blocked.
        Assert.NotEmpty(db.Predictions.Where(p => p.UserId == user));
        Assert.Contains(db.AuditLogs, a => a.Action == "SeasonUpdated" && a.EntityId == SeasonId.ToString());
    }
}
