using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Common;

/// <summary>
/// Builds catalogue rows for teams discovered in external data. Shared by the
/// fixture import (which auto-creates teams it cannot match) and the catalogue
/// sync, so both infer the same short name, Big Seven flag, division and team
/// type — a club created by one path must be indistinguishable from the other.
/// </summary>
public static class TeamFactory
{
    /// <summary>
    /// Creates a team from an external name found in <paramref name="competition"/>.
    /// The caller is responsible for having checked that no matching team exists
    /// (see <see cref="FootballReference.Canonical"/>) and for saving it.
    /// </summary>
    public static Team Create(string name, Competition competition)
    {
        var trimmed = name.Trim();
        var isNationalTeam = competition == Competition.FifaWorldCup;
        return new Team
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            ShortName = BuildShortName(trimmed),
            IsBigSevenClub = !isNationalTeam && FootballReference.IsBigSeven(trimmed),
            // Cup competitions draw from every division, so we can't infer the
            // club's league from an FA Cup fixture — leave it unset in that case.
            Division = competition is Competition.PremierLeague or Competition.Championship or Competition.LeagueOne
                ? competition
                : null,
            // World Cup fixtures create national teams (titles unknown for newly
            // discovered nations; the seven world champions are already seeded).
            TeamType = isNationalTeam ? TeamType.NationalTeam : TeamType.Club,
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>First three letters of the name, upper-cased ("TBD" when there are none).</summary>
    public static string BuildShortName(string name)
    {
        var letters = new string(name.Where(char.IsLetter).ToArray());
        var code = (letters.Length >= 3 ? letters[..3] : letters).ToUpperInvariant();
        return string.IsNullOrEmpty(code) ? "TBD" : code;
    }
}
