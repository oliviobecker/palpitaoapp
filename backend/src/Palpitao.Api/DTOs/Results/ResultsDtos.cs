using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Results;

/// <summary>A match result coming from an external results source.</summary>
public class ExternalMatchResultDto
{
    public string? ExternalMatchId { get; set; }
    public string? ExternalMatchUrl { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.NotStarted;
}

/// <summary>Response of the admin "refresh results" action.</summary>
public class RefreshResultsResponse
{
    public string Message { get; set; } = string.Empty;
    public Guid RoundId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public bool ProviderEnabled { get; set; }
    public int UpdatedMatches { get; set; }
    public int FinishedMatches { get; set; }
    public int InProgressMatches { get; set; }
    public int NotStartedMatches { get; set; }
    public int PostponedMatches { get; set; }
    public int CancelledMatches { get; set; }
    public DateTime? TemporaryStandingsUpdatedAt { get; set; }
}

public class TemporaryStandingDto
{
    public int Position { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RoundTemporaryPoints { get; set; }
    public int CurrentOfficialTotalPoints { get; set; }
    public int ProjectedTotalPoints { get; set; }
    public int ComputedMatches { get; set; }
    public int RemainingMatches { get; set; }
}

public class TemporaryStandingsDto
{
    public Guid RoundId { get; set; }
    public bool IsTemporary { get; set; } = true;
    public RoundStatus RoundStatus { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public int ComputedMatches { get; set; }
    public int RemainingMatches { get; set; }
    public List<TemporaryStandingDto> Standings { get; set; } = new();
}
