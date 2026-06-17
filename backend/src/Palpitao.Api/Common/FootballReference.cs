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

    public static bool IsBigSeven(string teamName)
        => !string.IsNullOrWhiteSpace(teamName) && BigSevenNames.Contains(Normalize(teamName));

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
