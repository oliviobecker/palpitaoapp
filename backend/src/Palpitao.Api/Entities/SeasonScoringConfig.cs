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

    // --- Special rules (Flávio Rule + absence punishments) -------------------
    // Defaults reproduce the historical hardcoded behaviour; the migration backfills
    // existing rows with the same numbers. Deliberately NOT mapped with
    // HasDefaultValue: AbsencePenaltyPoints = 0 is a legitimate choice, and a store
    // default would make EF omit the column on insert and silently persist 20.

    /// <summary>First round the Flávio Rule applies to (Palpitão England variant).</summary>
    public int FlavioFromRound { get; set; } = 16;

    /// <summary>First round in which an absence counts towards the punishment ladder.</summary>
    public int AbsenceFromRound { get; set; } = 1;

    /// <summary>Points deducted from the total per absence, from the 3rd one on.</summary>
    public int AbsencePenaltyPoints { get; set; } = 20;

    /// <summary>Absence ordinal that eliminates the participant from the season.</summary>
    public int AbsenceEliminationCount { get; set; } = 5;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public Season? Season { get; set; }
    public ICollection<ScoringScoreEntry> ScoreEntries { get; set; } = new List<ScoringScoreEntry>();
    public ICollection<ScoringMultiplierRule> MultiplierRules { get; set; } = new List<ScoringMultiplierRule>();
    public ICollection<ScoringClassicTeam> ClassicTeams { get; set; } = new List<ScoringClassicTeam>();
}
