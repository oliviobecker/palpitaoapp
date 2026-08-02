using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Teams;

/// <summary>Moves a club to another league division (null clears it).</summary>
public class UpdateTeamDivisionRequest
{
    public Competition? Division { get; set; }
}

/// <summary>A catalogue team as the admin screen sees it.</summary>
public class AdminTeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public bool IsBigSevenClub { get; set; }
    public string? CrestUrl { get; set; }
    public Competition? Division { get; set; }
    public TeamType TeamType { get; set; }
}

/// <summary>
/// Outcome of a catalogue sync. The same shape serves the preview and the applied
/// run: <see cref="Applied"/> says whether the changes were written.
/// </summary>
public class TeamSyncResponse
{
    public string Source { get; set; } = string.Empty;
    public bool Applied { get; set; }
    public List<TeamSyncCompetitionDto> Competitions { get; set; } = new();
}

public class TeamSyncCompetitionDto
{
    public Competition Competition { get; set; }

    /// <summary>
    /// How many distinct team names the source returned. Zero means the source told
    /// us nothing (off-season): nothing is classified and nothing is reported as
    /// missing, so the admin can tell "no changes" from "no data".
    /// </summary>
    public int FoundCount { get; set; }

    public List<TeamSyncCreateDto> ToCreate { get; set; } = new();
    public List<TeamSyncMoveDto> ToMove { get; set; } = new();

    /// <summary>Clubs the catalogue has in this division but the source did not list.
    /// Report-only — the sync never clears a division.</summary>
    public List<TeamSyncNotFoundDto> NotFound { get; set; } = new();

    public int UnchangedCount { get; set; }
}

public class TeamSyncCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public bool IsBigSevenClub { get; set; }
}

public class TeamSyncMoveDto
{
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Competition? FromDivision { get; set; }
    public Competition ToDivision { get; set; }
}

public class TeamSyncNotFoundDto
{
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}
