using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>
/// Assigns one normalized exact score to a difficulty category. Stored normalized as
/// (Low ≤ High) because the categories are symmetric (1x0 ≡ 0x1). Any exact score not
/// listed falls into <see cref="ScoreCategory.ExtraUncommon"/>.
/// </summary>
public class ScoringScoreEntry
{
    public Guid Id { get; set; }

    public Guid ConfigId { get; set; }

    /// <summary>Lower of the two goal counts (min(home, away)).</summary>
    public int Low { get; set; }

    /// <summary>Higher of the two goal counts (max(home, away)).</summary>
    public int High { get; set; }

    /// <summary>Category awarded for this exact score (Traditional / Medium / Uncommon).</summary>
    public ScoreCategory Category { get; set; }

    // Navigation
    public SeasonScoringConfig? Config { get; set; }
}
