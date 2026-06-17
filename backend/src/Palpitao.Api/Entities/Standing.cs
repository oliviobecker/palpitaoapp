namespace Palpitao.Api.Entities;

/// <summary>
/// Accumulated standing of a participant within a season (updated when rounds
/// are scored).
/// </summary>
public class Standing
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant). Defaults to the seeded default group.</summary>
    public Guid GroupId { get; set; }

    public Guid SeasonId { get; set; }

    public Guid UserId { get; set; }

    public int TotalPoints { get; set; }

    public int Position { get; set; }

    public int PlayedRounds { get; set; }

    public int ExactCount { get; set; }

    public int AbsenceCount { get; set; }

    /// <summary>Total points removed by penalties (3rd/4th absences).</summary>
    public int PenaltyPoints { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public Season? Season { get; set; }
    public User? User { get; set; }
}
