using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Predictions;

/// <summary>
/// "Participants view others' predictions" feature: the prediction mirror is gated by
/// the group's <see cref="Group.AllowParticipantsToViewOthersPredictions"/> setting for
/// participants, while group admins always see it (subject to the post-lock timing).
/// </summary>
public class MirrorVisibilityTests
{
    private static readonly Guid SeasonId = Guid.Parse("44444444-4444-4444-4444-444444444401");
    private static readonly CancellationToken Ct = CancellationToken.None;

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

    private static PredictionsService Service(AppDbContext db, GroupRole role)
        => new(db, new AuditService(db), new FakeCurrentGroupService(role: role));

    private static RoundService Rounds(AppDbContext db)
        => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static void SetVisibility(AppDbContext db, bool allowed)
    {
        db.Seasons.First(s => s.Id == SeasonId).AllowParticipantsToViewOthersPredictions = allowed;
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

    /// <summary>Creates a published round with one match, the participant's predictions, then sets the status.</summary>
    private static async Task<(Guid RoundId, Guid UserId)> RoundInStatus(AppDbContext db, RoundStatus status)
    {
        var rounds = Rounds(db);
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = 1 }, SeedIds.AdminUser, Ct);
        await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
        {
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = DateTime.UtcNow.AddDays(2),
        }, SeedIds.AdminUser, Ct);

        var detail = await rounds.GetByIdAsync(round.Id, Ct);
        var user = AddParticipant(db);

        if (status != RoundStatus.Draft)
        {
            await rounds.PublishAsync(round.Id, SeedIds.AdminUser, Ct);
            var participant = Service(db, GroupRole.Participant);
            await participant.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest
            {
                Predictions = detail.Matches.Select(m => new PredictionItemRequest
                {
                    RoundMatchId = m.Id,
                    PredictedHomeScore = 2,
                    PredictedAwayScore = 1,
                }).ToList(),
            }, isEdit: false, Ct);
        }

        if (status is RoundStatus.Locked or RoundStatus.Scored)
        {
            await rounds.LockAsync(round.Id, SeedIds.AdminUser, Ct);
        }

        if (status == RoundStatus.Scored)
        {
            db.Rounds.First(r => r.Id == round.Id).Status = RoundStatus.Scored;
            db.SaveChanges();
        }

        return (round.Id, user);
    }

    [Fact]
    public void Default_season_has_visibility_disabled()
    {
        using var db = CreateContext();
        Assert.False(db.Seasons.First(s => s.Id == SeasonId).AllowParticipantsToViewOthersPredictions);
    }

    [Fact]
    public async Task Participant_cannot_view_mirror_when_disabled()
    {
        using var db = CreateContext();
        SetVisibility(db, false);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Locked);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => Service(db, GroupRole.Participant).GetMirrorAsync(roundId, user, Ct));
        Assert.Equal("mirror.notAllowed", ex.Key);
    }

    [Fact]
    public async Task Participant_can_view_mirror_when_enabled_and_locked()
    {
        using var db = CreateContext();
        SetVisibility(db, true);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Locked);

        var mirror = await Service(db, GroupRole.Participant).GetMirrorAsync(roundId, user, Ct);
        Assert.Contains(mirror.Participants, p => p.UserId == user && p.Predictions.Count == 1);
    }

    [Fact]
    public async Task Participant_can_view_mirror_when_enabled_and_scored()
    {
        using var db = CreateContext();
        SetVisibility(db, true);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Scored);

        var mirror = await Service(db, GroupRole.Participant).GetMirrorAsync(roundId, user, Ct);
        Assert.Equal(RoundStatus.Scored, mirror.Status);
    }

    [Fact]
    public async Task Participant_can_view_mirror_before_lock_when_enabled()
    {
        using var db = CreateContext();
        SetVisibility(db, true);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Published);

        // Live visibility: with the season flag on, the mirror opens while the round
        // is still open (Published), before the lock.
        var mirror = await Service(db, GroupRole.Participant).GetMirrorAsync(roundId, user, Ct);
        Assert.Equal(RoundStatus.Published, mirror.Status);
        Assert.Contains(mirror.Participants, p => p.UserId == user && p.Predictions.Count == 1);
    }

    [Fact]
    public async Task Participant_cannot_view_mirror_before_lock_when_disabled()
    {
        using var db = CreateContext();
        SetVisibility(db, false);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Published);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => Service(db, GroupRole.Participant).GetMirrorAsync(roundId, user, Ct));
        Assert.Equal("mirror.notAllowed", ex.Key);
    }

    [Fact]
    public async Task Admin_cannot_view_mirror_before_lock_when_disabled()
    {
        using var db = CreateContext();
        SetVisibility(db, false);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Published);

        // Without the live flag the mirror stays private until the lock, even for admins.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db, GroupRole.GroupAdmin).GetMirrorAsync(roundId, user, Ct));
        Assert.Equal("mirror.afterLockOnly", ex.Key);
    }

    [Fact]
    public async Task Participant_cannot_view_mirror_in_draft_even_when_enabled()
    {
        using var db = CreateContext();
        SetVisibility(db, true);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Draft);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(db, GroupRole.Participant).GetMirrorAsync(roundId, user, Ct));
        Assert.Equal("mirror.afterLockOnly", ex.Key);
    }

    [Fact]
    public async Task Admin_can_view_mirror_when_disabled_and_locked()
    {
        using var db = CreateContext();
        SetVisibility(db, false);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Locked);

        var mirror = await Service(db, GroupRole.GroupAdmin).GetMirrorAsync(roundId, user, Ct);
        Assert.Contains(mirror.Participants, p => p.UserId == user);
    }

    [Fact]
    public async Task Mirror_of_round_in_another_group_is_not_found()
    {
        using var db = CreateContext();
        SetVisibility(db, true);
        var (roundId, user) = await RoundInStatus(db, RoundStatus.Locked);

        // A user whose current group is a different group cannot reach this round.
        var otherGroup = new FakeCurrentGroupService(groupId: Guid.NewGuid(), role: GroupRole.Participant);
        var service = new PredictionsService(db, new AuditService(db), otherGroup);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMirrorAsync(roundId, user, Ct));
    }
}
