using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Seasons;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Seasons;
using Palpitao.Api.Services.Users;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Admin;

public class AdminServicesTests
{
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static SeasonRequest Season(string name, bool active) => new()
    {
        Name = name,
        StartDate = new DateOnly(2025, 8, 1),
        EndDate = new DateOnly(2026, 5, 31),
        IsActive = active,
    };

    [Fact]
    public async Task Create_persists_the_tournament_type_and_update_keeps_it_fixed()
    {
        using var db = CreateContext();
        var service = new SeasonService(db, new AuditService(db), new FakeCurrentGroupService());

        var created = await service.CreateAsync(
            new SeasonRequest
            {
                Name = "Copa 2026",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 7, 31),
                IsActive = true,
                TournamentType = Palpitao.Api.Enums.TournamentType.FifaWorldCup,
            },
            Admin, Ct);
        Assert.Equal(Palpitao.Api.Enums.TournamentType.FifaWorldCup, created.TournamentType);

        // The certame type is fixed after creation: an update ignores a different
        // TournamentType and keeps the original.
        var updated = await service.UpdateAsync(created.Id,
            new SeasonRequest
            {
                Name = "Copa 2026",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 7, 31),
                IsActive = true,
                TournamentType = Palpitao.Api.Enums.TournamentType.PalpitaoEngland,
            },
            Admin, Ct);
        Assert.Equal(Palpitao.Api.Enums.TournamentType.FifaWorldCup, updated.TournamentType);
    }

    [Fact]
    public async Task Create_persists_the_settings_the_admin_turned_off()
    {
        using var db = CreateContext();
        var service = new SeasonService(db, new AuditService(db), new FakeCurrentGroupService());

        var request = Season("2025/2026", active: true);
        request.AllowParticipantsToSubmitPredictions = false;
        request.FaCupEnabled = false;
        var created = await service.CreateAsync(request, Admin, Ct);

        // Both settings default to true, so an insert that skipped them would come back
        // as true and look like the admin never turned them off. Read them as scalars: a
        // projection bypasses the change tracker and asserts what reached the database.
        var stored = await db.Seasons
            .Where(s => s.Id == created.Id)
            .Select(s => new { s.AllowParticipantsToSubmitPredictions, s.FaCupEnabled })
            .SingleAsync(Ct);
        Assert.False(stored.AllowParticipantsToSubmitPredictions);
        Assert.False(stored.FaCupEnabled);
    }

    [Fact]
    public async Task Fa_cup_toggle_round_trips_and_is_audited()
    {
        using var db = CreateContext();
        var service = new SeasonService(db, new AuditService(db), new FakeCurrentGroupService());

        var created = await service.CreateAsync(Season("2025/2026", active: true), Admin, Ct);
        Assert.True(created.FaCupEnabled);

        var request = Season("2025/2026", active: true);
        request.FaCupEnabled = false;
        var updated = await service.UpdateAsync(created.Id, request, Admin, Ct);

        Assert.False(updated.FaCupEnabled);
        Assert.False((await db.Seasons.FirstAsync(s => s.Id == created.Id, Ct)).FaCupEnabled);
        Assert.Contains(db.AuditLogs, a => a.Action == "SeasonUpdated" && a.Details!.Contains("FaCupEnabled"));

        // And back on again.
        request.FaCupEnabled = true;
        Assert.True((await service.UpdateAsync(created.Id, request, Admin, Ct)).FaCupEnabled);
    }

    [Fact]
    public async Task Activating_a_season_deactivates_the_others()
    {
        using var db = CreateContext();
        var service = new SeasonService(db, new AuditService(db), new FakeCurrentGroupService());

        var first = await service.CreateAsync(Season("2024/2025", active: true), Admin, Ct);
        var second = await service.CreateAsync(Season("2025/2026", active: true), Admin, Ct);

        var active = await db.Seasons.Where(s => s.IsActive).ToListAsync(Ct);
        Assert.Single(active);
        Assert.Equal(second.Id, active[0].Id);
        Assert.False((await db.Seasons.FirstAsync(s => s.Id == first.Id, Ct)).IsActive);
    }

    [Fact]
    public async Task Create_participant_hashes_password_and_rejects_duplicate_email()
    {
        using var db = CreateContext();
        var service = new UserAdminService(db, new AuditService(db), new FakeCurrentGroupService());

        var created = await service.CreateAsync(
            new CreateParticipantRequest { Name = "João", Email = "joao@palpitao.local", Password = "Senha@123" }, Admin, Ct);

        var stored = await db.Users.FirstAsync(u => u.Id == created.Id, Ct);
        Assert.NotEqual("Senha@123", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Senha@123", stored.PasswordHash));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            new CreateParticipantRequest { Name = "Outro", Email = "joao@palpitao.local", Password = "Senha@123" }, Admin, Ct));
    }
}
