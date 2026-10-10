using System.Security.Cryptography;
using System.Text;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Seed;

/// <summary>The global team catalogue: the tracked English clubs and the World Cup champion nations.</summary>
internal static class TeamCatalogSeed
{
    private static readonly HashSet<string> BigSevenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Arsenal", "Chelsea", "Liverpool", "Manchester City",
        "Manchester United", "Newcastle", "Tottenham",
    };

    /// <summary>
    /// Club catalogue (season 2026/2027): full rosters of the three tracked league divisions. The
    /// "Big Seven" keep their fixed seed ids (referenced by tests and the scoring rules); every
    /// other club gets a deterministic id derived from its name so the seed stays stable across
    /// migrations without hand-coding dozens of GUIDs.
    /// </summary>
    /// <remarks>
    /// Promotion/relegation is applied by editing the Division below and adding a migration.
    /// Because <see cref="DeterministicGuid"/> keys off the <em>name</em> only, moving a club
    /// between divisions keeps its id, so EF emits a one-column UpdateData. Clubs that drop out
    /// of the tracked divisions keep their row with a null Division -- deleting them would trip
    /// the Restrict FKs from RoundMatches and ScoringClassicTeams on any database that already
    /// holds history.
    /// </remarks>
    public static IEnumerable<Team> Clubs()
    {
        var clubs = new (Guid? Id, string Name, string Short, Competition? Division)[]
        {
            // Premier League — Big Seven (fixed ids)
            (SeedIds.Arsenal, "Arsenal", "ARS", Competition.PremierLeague),
            (SeedIds.Chelsea, "Chelsea", "CHE", Competition.PremierLeague),
            (SeedIds.Liverpool, "Liverpool", "LIV", Competition.PremierLeague),
            (SeedIds.ManchesterCity, "Manchester City", "MCI", Competition.PremierLeague),
            (SeedIds.ManchesterUnited, "Manchester United", "MUN", Competition.PremierLeague),
            (SeedIds.Newcastle, "Newcastle", "NEW", Competition.PremierLeague),
            (SeedIds.Tottenham, "Tottenham", "TOT", Competition.PremierLeague),
            // Premier League — remaining clubs
            (null, "Aston Villa", "AVL", Competition.PremierLeague),
            (null, "Bournemouth", "BOU", Competition.PremierLeague),
            (null, "Brentford", "BRE", Competition.PremierLeague),
            (null, "Brighton & Hove Albion", "BHA", Competition.PremierLeague),
            (null, "Coventry City", "COV", Competition.PremierLeague),
            (null, "Crystal Palace", "CRY", Competition.PremierLeague),
            (null, "Everton", "EVE", Competition.PremierLeague),
            (null, "Fulham", "FUL", Competition.PremierLeague),
            (null, "Hull City", "HUL", Competition.PremierLeague),
            (null, "Ipswich Town", "IPS", Competition.PremierLeague),
            (null, "Leeds United", "LEE", Competition.PremierLeague),
            (null, "Nottingham Forest", "NFO", Competition.PremierLeague),
            (null, "Sunderland", "SUN", Competition.PremierLeague),
            // Championship
            (null, "Birmingham City", "BIR", Competition.Championship),
            (null, "Blackburn Rovers", "BLB", Competition.Championship),
            (null, "Bolton Wanderers", "BOL", Competition.Championship),
            (null, "Bristol City", "BRC", Competition.Championship),
            (null, "Burnley", "BUR", Competition.Championship),
            (null, "Cardiff City", "CAR", Competition.Championship),
            (null, "Charlton Athletic", "CHA", Competition.Championship),
            (null, "Derby County", "DER", Competition.Championship),
            (null, "Lincoln City", "LIN", Competition.Championship),
            (null, "Middlesbrough", "MID", Competition.Championship),
            // Millwall and West Ham are the Championship classic pair (see ScoringDefaults).
            (null, "Millwall", "MIL", Competition.Championship),
            (null, "Norwich City", "NOR", Competition.Championship),
            (null, "Portsmouth", "POR", Competition.Championship),
            (null, "Preston North End", "PNE", Competition.Championship),
            (null, "Queens Park Rangers", "QPR", Competition.Championship),
            (null, "Sheffield United", "SHU", Competition.Championship),
            (null, "Southampton", "SOU", Competition.Championship),
            (null, "Stoke City", "STK", Competition.Championship),
            (null, "Swansea City", "SWA", Competition.Championship),
            (null, "Watford", "WAT", Competition.Championship),
            (null, "West Bromwich Albion", "WBA", Competition.Championship),
            (null, "West Ham United", "WHU", Competition.Championship),
            (null, "Wolverhampton Wanderers", "WOL", Competition.Championship),
            (null, "Wrexham", "WRE", Competition.Championship),
            // League One
            (null, "AFC Wimbledon", "WIM", Competition.LeagueOne),
            (null, "Barnsley", "BAR", Competition.LeagueOne),
            (null, "Blackpool", "BLA", Competition.LeagueOne),
            (null, "Bradford City", "BRA", Competition.LeagueOne),
            (null, "Bromley", "BRO", Competition.LeagueOne),
            (null, "Burton Albion", "BTN", Competition.LeagueOne),
            (null, "Cambridge United", "CAM", Competition.LeagueOne),
            (null, "Doncaster Rovers", "DON", Competition.LeagueOne),
            (null, "Huddersfield Town", "HUD", Competition.LeagueOne),
            (null, "Leicester City", "LEI", Competition.LeagueOne),
            (null, "Leyton Orient", "LEY", Competition.LeagueOne),
            (null, "Luton Town", "LUT", Competition.LeagueOne),
            (null, "Mansfield Town", "MNF", Competition.LeagueOne),
            (null, "Milton Keynes Dons", "MKD", Competition.LeagueOne),
            (null, "Notts County", "NOT", Competition.LeagueOne),
            (null, "Oxford United", "OXF", Competition.LeagueOne),
            (null, "Peterborough United", "PET", Competition.LeagueOne),
            (null, "Plymouth Argyle", "PLY", Competition.LeagueOne),
            (null, "Reading", "REA", Competition.LeagueOne),
            (null, "Sheffield Wednesday", "SHW", Competition.LeagueOne),
            (null, "Stevenage", "STV", Competition.LeagueOne),
            (null, "Stockport County", "STO", Competition.LeagueOne),
            (null, "Wigan Athletic", "WIG", Competition.LeagueOne),
            (null, "Wycombe Wanderers", "WYC", Competition.LeagueOne),
            // Outside the tracked divisions (relegated to League Two in 2025/26).
            // Kept with a null Division so historical rounds keep their FK targets;
            // they still show up for the FA Cup, which draws from every division.
            (null, "Exeter City", "EXE", null),
            (null, "Northampton Town", "NTH", null),
            (null, "Port Vale", "PVL", null),
            (null, "Rotherham United", "ROT", null),
        };

        return clubs.Select(c => new Team
        {
            Id = c.Id ?? DeterministicGuid(c.Name),
            Name = c.Name,
            ShortName = c.Short,
            IsBigSevenClub = BigSevenNames.Contains(c.Name),
            Division = c.Division,
            TeamType = TeamType.Club,
            CreatedAt = Seeded.At,
        });
    }

    /// <summary>
    /// World champion national teams (FIFA World Cup certames). A team with WorldCupTitles &gt; 0
    /// is a "campeã mundial", used by the World Cup classic (double) multiplier in the knockout stage.
    /// </summary>
    public static IEnumerable<Team> NationalTeams()
    {
        var champions = new (string Name, string Fifa, string Iso, int Titles)[]
        {
            ("Brazil", "BRA", "BR", 5),
            ("Germany", "GER", "DE", 4),
            ("Argentina", "ARG", "AR", 3),
            ("France", "FRA", "FR", 2),
            ("Uruguay", "URU", "UY", 2),
            ("Spain", "ESP", "ES", 2),
            ("England", "ENG", "GB", 1),
        };

        return champions.Select(c => new Team
        {
            Id = DeterministicGuid("nation:" + c.Name),
            Name = c.Name,
            ShortName = c.Fifa,
            IsBigSevenClub = false,
            Division = null,
            TeamType = TeamType.NationalTeam,
            CountryCode = c.Iso,
            FifaCode = c.Fifa,
            WorldCupTitles = c.Titles,
            CreatedAt = Seeded.At,
        });
    }

    /// <summary>
    /// Derives a stable <see cref="Guid"/> from a club name so seeded clubs keep the same primary
    /// key across migrations without hand-coding GUID literals. Pure function of the (lower-cased)
    /// name, so the model snapshot stays deterministic.
    /// </summary>
    private static Guid DeterministicGuid(string name)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("palpitao-team:" + name.ToLowerInvariant()));
        return new Guid(bytes);
    }
}
