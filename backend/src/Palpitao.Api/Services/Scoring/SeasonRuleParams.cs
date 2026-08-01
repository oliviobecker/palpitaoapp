namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// The season's admin-editable special rules: when the Flávio Rule kicks in and how absences
/// are punished. Kept apart from the points ruleset because the round/absence services need
/// these four numbers without paying for the multiplier table and the classic-team list.
/// </summary>
/// <param name="FlavioFromRound">First round the Flávio Rule applies to (England variant).</param>
/// <param name="AbsenceFromRound">First round in which an absence counts towards the ladder.</param>
/// <param name="AbsencePenaltyPoints">Points deducted from the total per absence, from the 3rd on.</param>
/// <param name="AbsenceEliminationCount">Absence ordinal that eliminates the participant.</param>
public sealed record SeasonRuleParams(
    int FlavioFromRound,
    int AbsenceFromRound,
    int AbsencePenaltyPoints,
    int AbsenceEliminationCount)
{
    /// <summary>The historical hardcoded behaviour — used when a season has no persisted config.</summary>
    public static SeasonRuleParams Defaults { get; } = new(
        ScoringDefaults.FlavioFromRound,
        ScoringDefaults.AbsenceFromRound,
        ScoringDefaults.AbsencePenaltyPoints,
        ScoringDefaults.AbsenceEliminationCount);
}
