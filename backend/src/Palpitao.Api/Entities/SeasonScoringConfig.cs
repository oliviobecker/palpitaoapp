namespace Palpitao.Api.Entities;

/// <summary>
/// Per-season snapshot of the editable scoring ruleset: base points per category, the
/// exact-score → category mapping, the multiplier table and the classic-eligible teams.
/// Group admins edit it; it is seeded from the tournament-type defaults. Implements
/// <see cref="IGroupOwned"/> so it inherits the multi-tenant global query filter and the
/// insert-time group stamping. One row per <see cref="Season"/>.
///
/// Snapshotting (rather than reading the live defaults / global <see cref="Team"/> flags)
/// keeps re-scoring deterministic and avoids one group's edits leaking across tenants.
/// </summary>
public class SeasonScoringConfig : IGroupOwned
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant).</summary>
    public Guid GroupId { get; set; }

    public Guid SeasonId { get; set; }

    // --- Base points per category (before the multiplier) -------------------
    public int ColumnOnlyPoints { get; set; }
    public int TraditionalPoints { get; set; }
    public int MediumPoints { get; set; }
    public int UncommonPoints { get; set; }
    public int ExtraUncommonPoints { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public Season? Season { get; set; }
    public ICollection<ScoringScoreEntry> ScoreEntries { get; set; } = new List<ScoringScoreEntry>();
    public ICollection<ScoringMultiplierRule> MultiplierRules { get; set; } = new List<ScoringMultiplierRule>();
    public ICollection<ScoringClassicTeam> ClassicTeams { get; set; } = new List<ScoringClassicTeam>();
}
