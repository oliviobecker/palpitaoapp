using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Xunit;

namespace Palpitao.Api.Tests.Data;

/// <summary>
/// Guards the public-key invariant enforced in <c>SaveChanges</c>. The column is unique and
/// required, so a season saved without a key is not just incomplete — the second one would
/// fail on the index. Stamping in the context keeps every write path correct, including the
/// ones that do not know the feature exists.
/// </summary>
public class SeasonPublicKeyStampTests
{
    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static Season NewSeason(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        StartDate = new DateOnly(2025, 8, 1),
        EndDate = new DateOnly(2026, 5, 31),
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void Seasons_saved_without_a_key_get_distinct_keys()
    {
        using var db = CreateContext();
        db.Seasons.Add(NewSeason("A"));
        db.Seasons.Add(NewSeason("B"));
        db.Seasons.Add(NewSeason("C"));

        db.SaveChanges();

        var keys = db.Seasons.Select(s => s.PublicKey).ToList();
        Assert.Equal(3, keys.Count);
        Assert.All(keys, k => Assert.Equal(12, k.Length));
        Assert.Equal(3, keys.Distinct().Count());
    }

    [Fact]
    public void An_explicit_key_is_never_overwritten()
    {
        using var db = CreateContext();
        var season = NewSeason("Explicit");
        season.PublicKey = "A7C39F2E4BD8";
        db.Seasons.Add(season);

        db.SaveChanges();

        Assert.Equal("A7C39F2E4BD8", db.Seasons.Single().PublicKey);
    }

    [Fact]
    public void Publishing_is_off_by_default()
    {
        using var db = CreateContext();
        db.Seasons.Add(NewSeason("Fresh"));

        db.SaveChanges();

        // Deploying the feature must not expose an existing season on its own.
        Assert.False(db.Seasons.Single().PublicStandingsEnabled);
    }
}
