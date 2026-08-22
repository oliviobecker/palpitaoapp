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

namespace Palpitao.Api.Tests.Admin;

/// <summary>
/// Activating a participant who was out while a round closed also records their absence for
/// that round, so the decision is explicit and survives a season recalculation.
/// </summary>
public class ParticipantActivationTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

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

    /// <summary>An inactive participant — the state the admin is about to reverse.</summary>
    private static Guid CreateInactiveParticipant(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = "João Paulo",
            Email = $"user-{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        TestSeed.AddDefaultGroupMembership(db, id, isActive: false);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> Round(AppDbContext db, int number, bool locked = true)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number }, Admin, Ct);
        await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
        {
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = Future,
        }, Admin, Ct);
        var published = await rounds.PublishAsync(round.Id, Admin, Ct);
        return locked ? await rounds.LockAsync(round.Id, Admin, Ct) : published;
    }

    /// <summary>Reads the per-group active flag as it reached the database, past the change tracker.</summary>
    private static bool StoredIsActive(AppDbContext db, Guid userId)
        => db.GroupUsers
            .Where(gu => gu.GroupId == SeedIds.DefaultGroup && gu.UserId == userId)
            .Select(gu => gu.IsActive)
            .Single();

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Activating_records_the_chosen_absences_in_one_commit()
    {
        using var db = CreateContext();
        var service = TestServices.UserAdmin(db);
        var user = CreateInactiveParticipant(db);
        var closed = await Round(db, 1);

        await service.SetActiveAsync(user, true, new[] { closed.Id }, Admin, Ct);

        Assert.True(StoredIsActive(db, user));
        var stored = db.AbsenceOverrides.Single(o => o.RoundId == closed.Id && o.UserId == user);
        Assert.True(stored.IsAbsent);
        Assert.Contains(db.AuditLogs, a => a.Action == "ParticipantActivated");
        Assert.Contains(db.AuditLogs, a => a.Action == "AbsenceOverride");
    }

    [Fact]
    public async Task Activating_uses_the_server_generated_justification()
    {
        using var db = CreateContext();
        var service = TestServices.UserAdmin(db);
        var user = CreateInactiveParticipant(db);
        var closed = await Round(db, 1);

        await service.SetActiveAsync(user, true, new[] { closed.Id }, Admin, Ct);

        // The justification is an audit artefact, so the client is never its source.
        var stored = db.AbsenceOverrides.Single(o => o.RoundId == closed.Id && o.UserId == user);
        Assert.Equal(DomainMessages.Resolve("absence.autoOnActivate", "en"), stored.Justification);
    }

    [Fact]
    public async Task Activating_with_an_ineligible_round_leaves_the_participant_inactive()
    {
        using var db = CreateContext();
        var service = TestServices.UserAdmin(db);
        var user = CreateInactiveParticipant(db);
        var stillOpen = await Round(db, 1, locked: false);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SetActiveAsync(user, true, new[] { stillOpen.Id }, Admin, Ct));

        // Nothing was committed: the flag flip and the overrides share one SaveChanges.
        Assert.False(StoredIsActive(db, user));
        Assert.Empty(db.AbsenceOverrides.Where(o => o.UserId == user));
    }

    [Fact]
    public async Task Activating_without_choices_only_flips_the_flag()
    {
        using var db = CreateContext();
        var service = TestServices.UserAdmin(db);
        var user = CreateInactiveParticipant(db);
        await Round(db, 1);

        // The admin unchecked everything — deliberately different from cancelling.
        await service.SetActiveAsync(user, true, [], Admin, Ct);

        Assert.True(StoredIsActive(db, user));
        Assert.Empty(db.AbsenceOverrides.Where(o => o.UserId == user));
    }

    [Fact]
    public async Task Deactivating_ignores_absent_round_ids()
    {
        using var db = CreateContext();
        var service = TestServices.UserAdmin(db);
        var user = CreateInactiveParticipant(db);
        var closed = await Round(db, 1);

        await service.SetActiveAsync(user, false, new[] { closed.Id }, Admin, Ct);

        Assert.False(StoredIsActive(db, user));
        Assert.Empty(db.AbsenceOverrides.Where(o => o.UserId == user));
    }
}
