using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Ocr;
using Xunit;

namespace Palpitao.Api.Tests.Ocr;

/// <summary>
/// Round-trip guard for the short team names the app prints in the copy-ready
/// WhatsApp messages (frontend <c>shared/utils/team-name.util.ts</c>). Participants
/// edit their scores into that message and reply on WhatsApp; the admin screenshots
/// the reply and imports it here, so every short name must parse and resolve back to
/// the club it was printed for.
///
/// The round is built from the <em>whole</em> seeded catalogue, so a short name that
/// is ambiguous (matches two clubs) fails here instead of silently degrading to a
/// manual-review row in production. The table mirrors the frontend one — adding an
/// entry there means adding one here, and a row in the
/// <see cref="Palpitao.Api.Common.FootballReference"/> alias map when the short name is
/// not a substring of the full name.
///
/// One documented exception: the frontend also shortens "Liverpool FC", the spelling the
/// fixture feed uses rather than a seeded club. It has no row here because every full name
/// below must exist in the catalogue; that spelling is covered by
/// <c>PredictionImportServiceTests</c> instead.
/// </summary>
public class OcrShortNameRoundTripTests
{
    private const string OpponentName = "Arsenal";

    /// <summary>Seeded <c>Team.Name</c> → short name printed in the messages.</summary>
    private static readonly (string Full, string Short)[] ShortNames =
    [
        ("AFC Wimbledon", "Wimbledon"),
        ("Birmingham City", "Birmingham"),
        ("Blackburn Rovers", "Blackburn"),
        ("Bolton Wanderers", "Bolton"),
        ("Bradford City", "Bradford"),
        ("Brighton & Hove Albion", "Brighton"),
        ("Burton Albion", "Burton"),
        ("Cambridge United", "Cambridge"),
        ("Cardiff City", "Cardiff"),
        ("Charlton Athletic", "Charlton"),
        ("Coventry City", "Coventry"),
        ("Derby County", "Derby"),
        ("Doncaster Rovers", "Doncaster"),
        ("Huddersfield Town", "Huddersfield"),
        ("Hull City", "Hull"),
        ("Ipswich Town", "Ipswich"),
        ("Leeds United", "Leeds"),
        ("Leicester City", "Leicester"),
        ("Lincoln City", "Lincoln"),
        ("Luton Town", "Luton"),
        ("Manchester City", "Man City"),
        ("Manchester United", "Man Utd"),
        ("Mansfield Town", "Mansfield"),
        ("Milton Keynes Dons", "MK Dons"),
        ("Norwich City", "Norwich"),
        ("Nottingham Forest", "Nottingham"),
        ("Oxford United", "Oxford"),
        ("Peterborough United", "Peterborough"),
        ("Plymouth Argyle", "Plymouth"),
        ("Preston North End", "Preston"),
        ("Queens Park Rangers", "QPR"),
        ("Sheffield United", "Sheffield Utd"),
        ("Sheffield Wednesday", "Sheffield Weds"),
        ("Stockport County", "Stockport"),
        ("Stoke City", "Stoke"),
        ("Swansea City", "Swansea"),
        ("West Bromwich Albion", "West Brom"),
        ("West Ham United", "West Ham"),
        ("Wigan Athletic", "Wigan"),
        ("Wolverhampton Wanderers", "Wolves"),
        ("Wycombe Wanderers", "Wycombe"),
    ];

    public static TheoryData<string, string> Pairs
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (full, shortName) in ShortNames)
            {
                data.Add(shortName, full);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Short_name_resolves_back_to_its_club_at_home(string shortName, string fullName)
    {
        var (matches, idByName) = RoundOfEveryClub(clubAtHome: true);

        var parsed = Assert.Single(OcrTextParser.Parse($"{shortName} 2 x 1 {OpponentName}"));

        Assert.Equal(
            idByName[fullName],
            OcrTeamMatcher.ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, matches));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Short_name_resolves_back_to_its_club_away(string shortName, string fullName)
    {
        // Worth repeating away: CleanTeam trims the leading and trailing edges with
        // different rules, and an all-caps token like "QPR" is only confident leading.
        var (matches, idByName) = RoundOfEveryClub(clubAtHome: false);

        var parsed = Assert.Single(OcrTextParser.Parse($"{OpponentName} 2 x 1 {shortName}"));

        Assert.Equal(
            idByName[fullName],
            OcrTeamMatcher.ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, matches));
    }

    [Fact]
    public void Every_abbreviated_club_is_still_in_the_seeded_catalogue()
    {
        // Renaming a club in AppDbContext must fail here, not in production.
        var seeded = SeededTeamNames.Value;

        var missing = ShortNames.Select(p => p.Full).Where(n => !seeded.Contains(n)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void No_club_fuzzy_matches_a_different_club()
    {
        // The safety proof for the fuzzy tier in OcrTeamMatcher. Each club is *removed* from
        // the round before its own name is looked up — otherwise it matches itself strictly and
        // the fuzzy tier never runs, so the test would pass without proving anything. With it
        // absent the strict pass finds nothing, fuzzy runs against every other club, and must
        // still resolve to nobody.
        //
        // A failure means the edit-distance budget is too generous: tighten it in
        // OcrTeamMatcher.Fuzzy, do not allowlist the pair here. The tight ones today are
        // Luton/Leyton and Barnsley/Burnley, both exactly 2 edits apart against a budget of 1.
        var shortByFull = ShortNames.ToDictionary(p => p.Full, p => p.Short);

        var collisions = new List<string>();
        foreach (var name in SeededTeamNames.Value)
        {
            var (matches, idByName) = RoundOfEveryClub(clubAtHome: true, except: name);
            string[] written = shortByFull.TryGetValue(name, out var s) ? [name, s] : [name];

            foreach (var form in written)
            {
                var parsed = Assert.Single(OcrTextParser.Parse($"{form} 2 x 1 {OpponentName}"));
                var resolved = OcrTeamMatcher.ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, matches);
                if (resolved is not null)
                {
                    var other = idByName.First(kv => kv.Value == resolved).Key;
                    collisions.Add($"'{form}' ({name}) fuzzy-matched '{other}'");
                }
            }
        }

        Assert.Empty(collisions);
    }

    [Fact]
    public void Every_alias_resolves_to_its_own_club_and_to_no_other()
    {
        // The fuzzy tier reaches a club through the alias map (so "Weolves" can find
        // Wolverhampton Wanderers, which is nowhere near it by edit distance). That bridge is
        // only safe while each alias points at exactly one club: with its own club present the
        // alias must find it, and with that club removed it must find nobody rather than
        // dragging the name onto a neighbour.
        var wrong = new List<string>();
        foreach (var (alias, canonical) in FootballReference.Aliases)
        {
            if (!SeededTeamNames.Value.Contains(canonical, StringComparer.OrdinalIgnoreCase))
            {
                continue; // A spelling only the fixture feed uses; it has no seeded club.
            }

            var (matches, idByName) = RoundOfEveryClub(clubAtHome: true);
            var seeded = idByName.Keys.First(n => string.Equals(n, canonical, StringComparison.OrdinalIgnoreCase));
            var resolved = OcrTeamMatcher.ResolveMatch(alias, OpponentName, matches);
            if (resolved != idByName[seeded])
            {
                var other = idByName.FirstOrDefault(kv => kv.Value == resolved).Key ?? "nobody";
                wrong.Add($"'{alias}' resolved to '{other}' instead of '{seeded}'");
            }

            var (without, idsWithout) = RoundOfEveryClub(clubAtHome: true, except: seeded);
            var stray = OcrTeamMatcher.ResolveMatch(alias, OpponentName, without);
            if (stray is not null)
            {
                wrong.Add($"'{alias}' fuzzy-matched '{idsWithout.First(kv => kv.Value == stray).Key}'");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void Full_names_still_resolve_so_older_screenshots_keep_importing()
    {
        var (matches, idByName) = RoundOfEveryClub(clubAtHome: true);

        foreach (var (full, _) in ShortNames)
        {
            var parsed = Assert.Single(OcrTextParser.Parse($"{full} 2 x 1 {OpponentName}"));
            Assert.Equal(
                idByName[full],
                OcrTeamMatcher.ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, matches));
        }
    }

    /// <summary>
    /// One fixture per seeded club, all against the same opponent, so the only thing
    /// that can pick a match is the club side of the line. A short name that also fits
    /// another club returns null from ResolveMatch and fails the assertion.
    /// </summary>
    private static (List<RoundMatch> Matches, Dictionary<string, Guid> IdByName) RoundOfEveryClub(
        bool clubAtHome, string? except = null)
    {
        var matches = new List<RoundMatch>();
        var idByName = new Dictionary<string, Guid>();
        foreach (var name in SeededTeamNames.Value.Where(n => n != except))
        {
            var id = Guid.NewGuid();
            idByName[name] = id;
            var club = new Team { Name = name };
            var opponent = new Team { Name = OpponentName };
            matches.Add(new RoundMatch
            {
                Id = id,
                HomeTeam = clubAtHome ? club : opponent,
                AwayTeam = clubAtHome ? opponent : club,
            });
        }

        return (matches, idByName);
    }

    private static readonly Lazy<IReadOnlyList<string>> SeededTeamNames = new(() =>
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db.Teams.AsNoTracking().Select(t => t.Name).ToList();
    });
}
