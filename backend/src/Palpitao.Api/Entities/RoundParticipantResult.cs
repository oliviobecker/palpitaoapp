namespace Palpitao.Api.Entities;

/// <summary>
/// Per-round result of a participant (gross points, penalties, final points and
/// status flags). Populated when a round is scored / processed.
/// </summary>
public class RoundParticipantResult : IGroupOwned
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant). Defaults to the seeded default group.</summary>
    public Guid GroupId { get; set; }

    public Guid SeasonId { get; set; }

    public Guid RoundId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Sum of the per-match points before penalties / Flávio rule.</summary>
    public int GrossPoints { get; set; }

    /// <summary>Points that actually count for the round (after Flávio rule).</summary>
    public int FinalPoints { get; set; }

    /// <summary>Points removed from the season total (e.g. 3rd/4th absence = 20).</summary>
    public int PenaltyPoints { get; set; }

    public bool WasAbsent { get; set; }

    public bool WasEliminated { get; set; }

    public bool FlavioRuleApplied { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public Season? Season { get; set; }
    public Round? Round { get; set; }
    public User? User { get; set; }
}
