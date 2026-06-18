namespace Palpitao.Api.DTOs.Scouts;

/// <summary>
/// Scout of a round: for each match, the participants grouped by the exact
/// scoreline they predicted. Used to build the copy-ready group "Scout" message.
/// </summary>
public class RoundScoutDto
{
    public Guid RoundId { get; set; }
    public int RoundNumber { get; set; }
    public string? RoundTitle { get; set; }
    public List<ScoutMatchDto> Matches { get; set; } = new();
}

public class ScoutMatchDto
{
    public Guid RoundMatchId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;

    /// <summary>Distinct predicted scorelines, each with the participants who chose it.</summary>
    public List<ScoutScoreGroupDto> Groups { get; set; } = new();
}

public class ScoutScoreGroupDto
{
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public List<string> Names { get; set; } = new();
}
