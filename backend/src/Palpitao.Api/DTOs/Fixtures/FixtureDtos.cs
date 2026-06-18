using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Fixtures;

/// <summary>Request to search external fixtures available in a period.</summary>
public class SearchFixturesRequest
{
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Competitions to include. When empty, all four tracked competitions are used.
    /// </summary>
    public List<Competition> Competitions { get; set; } = new();

    /// <summary>
    /// Optional round being edited; used to flag fixtures already added to it.
    /// </summary>
    public Guid? RoundId { get; set; }
}

/// <summary>A candidate fixture returned by an <c>IFixtureProvider</c>.</summary>
public class FixtureCandidateDto
{
    public string ExternalId { get; set; } = string.Empty;
    public Competition Competition { get; set; }
    public MatchPhase Phase { get; set; } = MatchPhase.Regular;
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsBigSevenMatch { get; set; }
    public int SuggestedMultiplier { get; set; } = 1;
    public bool IsAlreadyAddedToRound { get; set; }
}

public class SearchFixturesResponse
{
    public string Source { get; set; } = string.Empty;
    public List<FixtureCandidateDto> Fixtures { get; set; } = new();
}

/// <summary>A single fixture the admin selected to import into a round.</summary>
public class ImportFixtureItem
{
    public string ExternalId { get; set; } = string.Empty;

    public Competition Competition { get; set; }

    public MatchPhase Phase { get; set; } = MatchPhase.Regular;

    public string HomeTeamName { get; set; } = string.Empty;

    public string AwayTeamName { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    public string? Source { get; set; }
}

public class ImportFixturesRequest
{
    public List<ImportFixtureItem> Fixtures { get; set; } = new();

    /// <summary>
    /// Required to import more than one League One match into the same round.
    /// </summary>
    public string? LeagueOneJustification { get; set; }
}

public class ImportFixturesResponse
{
    public int ImportedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public int CreatedTeamCount { get; set; }
    public List<string> SkippedDuplicates { get; set; } = new();
}
