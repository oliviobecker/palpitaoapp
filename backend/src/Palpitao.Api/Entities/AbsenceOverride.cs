namespace Palpitao.Api.Entities;

/// <summary>
/// Manual admin override of the automatic absence detection for a participant in
/// a round (e.g. to excuse a participant), with a mandatory justification.
/// </summary>
public class AbsenceOverride
{
    public Guid Id { get; set; }

    public Guid RoundId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The forced value: true = treat as absent, false = treat as present.</summary>
    public bool IsAbsent { get; set; }

    public string Justification { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Round? Round { get; set; }
    public User? User { get; set; }
}
