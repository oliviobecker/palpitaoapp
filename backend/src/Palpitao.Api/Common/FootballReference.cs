using Palpitao.Api.Enums;

namespace Palpitao.Api.Common;

/// <summary>
/// Static football reference data shared by the fixture import pipeline:
/// the "Big Seven" clubs and the mapping between external competition labels
/// and the internal <see cref="Competition"/> enum.
/// </summary>
public static class FootballReference
{
    /// <summary>
    /// Canonical "Big Seven" club names (lower-cased, accent-free) and common
    /// aliases used by external sources. Used to flag <c>IsBigSevenClub</c> when
    /// a team is created automatically during import.
    /// </summary>
    private static readonly HashSet<string> BigSevenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "arsenal",
        "chelsea",
        "liverpool",
        "liverpool fc",
        "manchester city",
        "man city",
        "manchester united",
        "man united",
        "man utd",
        "newcastle",
        "newcastle united",
        "tottenham",
        "tottenham hotspur",
        "spurs",
    };

    /// <summary>
    /// Canonical names (and aliases) of the default Championship classic pair, mirroring
    /// <c>SeasonScoringConfigService</c>'s default groups.
    /// </summary>
    private static readonly HashSet<string> ChampionshipClassicNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "millwall",
        "millwall fc",
        "west ham",
        "west ham united",
    };

    public static bool IsBigSeven(string teamName)
        => !string.IsNullOrWhiteSpace(teamName) && BigSevenNames.Contains(Normalize(teamName));

    public static bool IsChampionshipClassic(string teamName)
        => !string.IsNullOrWhiteSpace(teamName) && ChampionshipClassicNames.Contains(Normalize(teamName));

    /// <summary>
    /// True when the two teams form a classic: both from the Big Seven, or both from the
    /// Championship pair. Name-based because the import runs before the teams exist in the
    /// catalogue — the season's own classic groups take over once the match is saved.
    /// </summary>
    public static bool IsClassicPair(string homeTeamName, string awayTeamName)
        => (IsBigSeven(homeTeamName) && IsBigSeven(awayTeamName))
            || (IsChampionshipClassic(homeTeamName) && IsChampionshipClassic(awayTeamName));

    /// <summary>
    /// Maps the name variants external sources use onto the catalogue's canonical
    /// club name (keys and values both already normalized). Providers each spell
    /// clubs their own way; without this, import fails to match the existing row
    /// and silently auto-creates a duplicate team.
    /// </summary>
    /// <remarks>
    /// Invariant: no value may also appear as a key, so <see cref="Canonical"/> is
    /// idempotent and safe to apply to both sides of a comparison. Enforced by test.
    /// Deliberately stricter than <c>OcrTeamMatcher.TeamAliases</c>, which maps bare
    /// "city"/"united" — acceptable in that fuzzy, human-reviewed flow but wrong
    /// here (Bristol City, Hull City, Leeds United, Oxford United, …).
    /// </remarks>
    private static readonly Dictionary<string, string> NameAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["afc bournemouth"] = "bournemouth",
        ["brighton"] = "brighton & hove albion",
        ["brighton and hove albion"] = "brighton & hove albion",
        ["leeds"] = "leeds united",
        ["liverpool fc"] = "liverpool",
        ["man city"] = "manchester city",
        ["man united"] = "manchester united",
        ["man utd"] = "manchester united",
        ["mk dons"] = "milton keynes dons",
        ["newcastle united"] = "newcastle",
        ["nott'm forest"] = "nottingham forest",
        ["nottm forest"] = "nottingham forest",
        ["peterborough"] = "peterborough united",
        ["preston"] = "preston north end",
        ["qpr"] = "queens park rangers",
        ["sheffield utd"] = "sheffield united",
        ["sheffield weds"] = "sheffield wednesday",
        ["spurs"] = "tottenham",
        ["tottenham hotspur"] = "tottenham",
        ["west brom"] = "west bromwich albion",
        ["west ham"] = "west ham united",
        ["wimbledon"] = "afc wimbledon",
        ["wolves"] = "wolverhampton wanderers",
    };

    /// <summary>Alias map, exposed so tests can assert the idempotence invariant.</summary>
    public static IReadOnlyDictionary<string, string> Aliases => NameAliases;

    /// <summary>
    /// Normalizes a team name and resolves known external spellings to the
    /// catalogue's canonical name. Use this — not <see cref="Normalize"/> — whenever
    /// an external team name is compared against a stored one.
    /// </summary>
    public static string Canonical(string name)
    {
        var normalized = Normalize(name);
        return NameAliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    /// <summary>Normalizes a team name for comparison (trim + collapse spaces + lower).</summary>
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed.ToLowerInvariant();
    }

    /// <summary>
    /// Maps an external competition label to the internal enum. Returns null when
    /// the competition is not one of the four the system tracks (so the caller can
    /// ignore it).
    /// </summary>
    public static Competition? MapCompetition(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var key = Normalize(label).Replace("-", " ");
        return key switch
        {
            "premier league" or "premierleague" or "english premier league" or "epl" => Competition.PremierLeague,
            "fa cup" or "facup" or "the fa cup" or "copa da inglaterra" => Competition.FACup,
            "championship" or "efl championship" or "sky bet championship" => Competition.Championship,
            "league one" or "leagueone" or "efl league one" or "sky bet league one" => Competition.LeagueOne,
            _ => null,
        };
    }
}
