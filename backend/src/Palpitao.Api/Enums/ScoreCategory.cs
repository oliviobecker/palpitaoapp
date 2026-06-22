namespace Palpitao.Api.Enums;

/// <summary>
/// Category awarded to a prediction after a round is scored.
/// The concrete point values are defined by the scoring rules (later phase).
/// </summary>
public enum ScoreCategory
{
    None,
    ColumnOnly,
    Traditional,
    Medium,
    Uncommon,
    ExtraUncommon,
}
