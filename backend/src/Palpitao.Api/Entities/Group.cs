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
