using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

public class Round
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant), denormalized from <see cref="Season"/> so
    /// every per-round entity reaches the tenant in a single join.</summary>
    public Guid GroupId { get; set; }

    public Guid SeasonId { get; set; }

    public int Number { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// Start of the round's window. Used to search external fixtures for the
    /// period. Optional for legacy rounds; required by the create/edit UI.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>End of the round's window (must be >= <see cref="StartDate"/>).</summary>
    public DateTime? EndDate { get; set; }

    public RoundStatus Status { get; set; } = RoundStatus.Draft;

    /// <summary>
    /// General lock / prediction deadline: start of the round's earliest match.
    /// Calculated when the round is published.
    /// </summary>
    public DateTime? FirstMatchStartsAt { get; set; }

    /// <summary>Set when the round transitions Draft -> Published.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Set when the round transitions Published -> Locked.</summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>Timestamp of the last results refresh (for the temporary standings).</summary>
    public DateTime? ResultsUpdatedAt { get; set; }

    /// <summary>
    /// Reference timestamp for the "Regra Flávio" special deadline (publication of
    /// the predictions mirror in the official group). Falls back to PublishedAt.
    /// </summary>
    public DateTime? MirrorPublishedAt { get; set; }

    /// <summary>Computed special deadline for the leader(s) (Regra Flávio).</summary>
    public DateTime? FlavioDeadlineUtc { get; set; }

    /// <summary>True when the general lock prevails over the Flávio deadline (admin alert).</summary>
    public bool FlavioConflictAlert { get; set; }

    /// <summary>
    /// Whether the Regra Flávio applies to this round. Determined at publication
    /// from the certame type (England: round number ≥ 16; World Cup: a match from
    /// the quarter-finals on).
    /// </summary>
    public bool FlavioRuleApplies { get; set; }

    /// <summary>
    /// Leader of the general standing captured at publication — the Flávio rule's
    /// target. Persisted so a mid-round standings change does not move the target.
    /// </summary>
    public Guid? FlavioRuleTargetUserId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public Season? Season { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<RoundMatch> Matches { get; set; } = new List<RoundMatch>();
    public ICollection<Absence> Absences { get; set; } = new List<Absence>();
}
