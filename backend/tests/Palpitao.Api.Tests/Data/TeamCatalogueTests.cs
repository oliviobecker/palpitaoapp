using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Enums;
using Xunit;

namespace Palpitao.Api.Tests.Data;

/// <summary>
/// Guards the seeded global club catalogue. It reflects a real-world season, so it
/// goes stale every August — these assertions are meant to fail loudly when the
/// rosters are not refreshed, rather than letting the admin quietly build rounds
/// from last season's divisions.
/// </summary>
public class TeamCatalogueTests
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

    [Theory]
    [InlineData(Competition.PremierLeague, 20)]
    [InlineData(Competition.Championship, 24)]
    [InlineData(Competition.LeagueOne, 24)]
    public void Each_tracked_division_has_a_full_roster(Competition division, int expected)
    {
        using var db = CreateContext();
        Assert.Equal(expected, db.Teams.Count(t => t.Division == division));
    }

    [Fact]
    public void Clubs_outside_the_tracked_divisions_are_kept_without_a_division()
    {
        using var db = CreateContext();
        // Relegated to League Two: kept (never deleted) because RoundMatch and
        // ScoringClassicTeam hold Restrict FKs into Teams.
        foreach (var name in new[] { "Exeter City", "Northampton Town", "Port Vale", "Rotherham United" })
        {
            var club = db.Teams.Single(t => t.Name == name);
            Assert.Null(club.Division);
            Assert.Equal(TeamType.Club, club.TeamType);
        }
    }

    [Fact]
    public void Big_seven_are_exactly_seven_and_all_in_the_premier_league()
    {
        using var db = CreateContext();
        var bigSeven = db.Teams.Where(t => t.IsBigSevenClub).ToList();
        Assert.Equal(7, bigSeven.Count);
        Assert.All(bigSeven, t => Assert.Equal(Competition.PremierLeague, t.Division));
    }

    [Fact]
    public void World_champions_carry_their_current_title_counts()
    {
        using var db = CreateContext();
        var champions = db.Teams
            .Where(t => t.TeamType == TeamType.NationalTeam && t.WorldCupTitles > 0)
            .ToDictionary(t => t.Name, t => t.WorldCupTitles);

        Assert.Equal(7, champions.Count);
        Assert.Equal(5, champions["Brazil"]);
        Assert.Equal(4, champions["Germany"]);
        Assert.Equal(3, champions["Argentina"]);
        Assert.Equal(2, champions["France"]);
        Assert.Equal(2, champions["Uruguay"]);
        Assert.Equal(2, champions["Spain"]);   // 2010 + 2026
        Assert.Equal(1, champions["England"]);
    }

    [Fact]
    public void Club_names_are_unique()
    {
        using var db = CreateContext();
        var names = db.Teams.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
