using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

public class Team
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Marks the "Big Seven" clubs of the season, used by the scoring rules
    /// (a prediction's category can depend on whether a big club is involved).
    /// </summary>
    public bool IsBigSevenClub { get; set; }

    /// <summary>
    /// The league division the club plays in for the season (Premier League,
    /// Championship or League One). Used to filter the team dropdown by the
    /// selected competition. Null for clubs not tied to a tracked division;
    /// cup competitions (FA Cup) draw from every division, so they are not
    /// filtered by this value.
    /// </summary>
    public Competition? Division { get; set; }

    public string? CrestUrl { get; set; }

    // --- National-team fields (FIFA World Cup certames) ----------------------

    /// <summary>Club (default) or national team. Clubs use <see cref="Division"/>;
    /// national teams use the fields below.</summary>
    public TeamType TeamType { get; set; } = TeamType.Club;

    /// <summary>ISO country code (e.g. "BR"), for national teams.</summary>
    public string? CountryCode { get; set; }

    /// <summary>FIFA tri-code (e.g. "BRA"), for national teams.</summary>
    public string? FifaCode { get; set; }

    /// <summary>Number of FIFA World Cup titles. A team with &gt; 0 is a world
    /// champion, used for the World Cup "classic" (campeãs mundiais) multiplier.</summary>
    public int WorldCupTitles { get; set; }

    /// <summary>True when the team has at least one World Cup title.</summary>
    public bool IsWorldChampion => WorldCupTitles > 0;

    public DateTime CreatedAt { get; set; }
}
