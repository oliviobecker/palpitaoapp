using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>
/// A group is an independent bolão/competition (the multi-tenant boundary). Each
/// group owns its own seasons, rounds, predictions, standings, OCR imports, audit
/// and membership; data never crosses groups.
/// </summary>
public class Group
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The kind of certame this group runs. Determines allowed competitions/phases,
    /// scoring multipliers and the Regra Flávio variant. Existing groups default to
    /// <see cref="TournamentType.PalpitaoEngland"/>.
    /// </summary>
    public TournamentType TournamentType { get; set; } = TournamentType.PalpitaoEngland;

    /// <summary>
    /// When true, approved participants may view the other participants' predictions
    /// (the prediction mirror), still subject to the round being locked/scored. When
    /// false (default), only group admins can. Off by default for privacy.
    /// </summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>
    /// When true (default), approved participants submit/edit their own predictions in
    /// the app. When false, only the admin enters predictions (manual / OCR) and the
    /// participant submission endpoints are blocked (403); existing predictions are kept.
    /// </summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>URL-friendly unique identifier, derived from the name.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>User who created the group (audit).</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Principal administrator of the group.</summary>
    public Guid OwnerUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<GroupUser> Members { get; set; } = new List<GroupUser>();
}
