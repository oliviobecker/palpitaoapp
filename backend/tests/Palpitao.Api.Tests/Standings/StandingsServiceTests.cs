using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Standings;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Standings;

public class StandingsServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly CancellationToken Ct = CancellationToken.None;

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
        // Two rounds to reference from results.
        for (var i = 1; i <= 2; i++)
        {
            db.Rounds.Add(new Round
            {
                Id = RoundId(i),
                SeasonId = SeasonId,
                Number = i,
                Status = RoundStatus.Scored,
                CreatedByUserId = SeedIds.AdminUser,
                CreatedAt = DateTime.UtcNow,
            });
        }
        db.SaveChanges();
        return db;
    }

    private static Guid RoundId(int n) => Guid.Parse($"44444444-4444-4444-4444-44444444440{n}");

    private static Guid CreateParticipant(AppDbContext db, string name)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"user-{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    private static void AddResult(AppDbContext db, int round, Guid userId, int final, int penalty = 0, bool absent = false)
    {
        var now = DateTime.UtcNow;
        db.RoundParticipantResults.Add(new RoundParticipantResult
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            RoundId = RoundId(round),
            UserId = userId,
            GrossPoints = final,
            FinalPoints = final,
            PenaltyPoints = penalty,
            WasAbsent = absent,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Orders_by_total_points_desc()
    {
        using var db = CreateContext();
        var service = new StandingsService(db, new FakeCurrentGroupService());
        var a = CreateParticipant(db, "Ana");
        var b = CreateParticipant(db, "Bruno");
        AddResult(db, 1, a, final: 10);
        AddResult(db, 1, b, final: 20);

        await service.RecomputeSeasonStandingsAsync(SeasonId, Ct);
        var standings = await service.GetStandingsAsync(SeasonId, Ct);

        Assert.Equal(b, standings[0].UserId);
        Assert.Equal(1, standings[0].Position);
        Assert.Equal(a, standings[1].UserId);
    }

    [Fact]
    public async Task Ties_break_by_fewer_absences()
    {
        using var db = CreateContext();
        var service = new StandingsService(db, new FakeCurrentGroupService());
        var a = CreateParticipant(db, "Ana");
        var b = CreateParticipant(db, "Bruno");

        // Both total 10, but A has one absence.
        AddResult(db, 1, a, final: 10);
        AddResult(db, 2, a, final: 0, absent: true);
        AddResult(db, 1, b, final: 10);

        await service.RecomputeSeasonStandingsAsync(SeasonId, Ct);
        var standings = await service.GetStandingsAsync(SeasonId, Ct);

        Assert.Equal(b, standings[0].UserId); // fewer absences ranks higher
        Assert.Equal(a, standings[1].UserId);
    }

    [Fact]
    public async Task Final_tie_breaks_by_name()
    {
        using var db = CreateContext();
        var service = new StandingsService(db, new FakeCurrentGroupService());
        var bruno = CreateParticipant(db, "Bruno");
        var ana = CreateParticipant(db, "Ana");

        // Same points, same absences -> alphabetical by name.
        AddResult(db, 1, bruno, final: 10);
        AddResult(db, 1, ana, final: 10);

        await service.RecomputeSeasonStandingsAsync(SeasonId, Ct);
        var standings = await service.GetStandingsAsync(SeasonId, Ct);

        Assert.Equal(ana, standings[0].UserId);
        Assert.Equal(bruno, standings[1].UserId);
    }
}
