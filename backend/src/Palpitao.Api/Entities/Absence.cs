namespace Palpitao.Api.Entities;

/// <summary>
/// Records that a participant did not submit predictions for a round, along with
/// the fixed negative penalty applied.
/// </summary>
public class Absence
{
    public Guid Id { get; set; }

    public Guid RoundId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Ordinal of this absence within the season (1st, 2nd, ...).</summary>
    public int AbsenceNumber { get; set; }

    /// <summary>Points removed from the season total for this absence (0, or 20 on 3rd/4th).</summary>
    public int PenaltyPoints { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Round? Round { get; set; }
    public User? User { get; set; }
}
