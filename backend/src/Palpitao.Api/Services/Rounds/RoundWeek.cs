using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Rounds;

/// <summary>
/// A round played in parts ("10.1", "10.2") — the lists of a week with two rounds — counts as
/// one round for absences. The parts share <see cref="Round.Number"/>; the last part that is
/// not cancelled (the highest <see cref="Round.Part"/>) decides the whole round, and a
/// participant is absent only when they sent nothing in every part. A standalone round
/// (<c>Part == 0</c>) is a round of its own and decides for itself.
/// </summary>
/// <remarks>
/// "Week" rather than "group" on purpose: a group is the tenant everywhere else.
/// </remarks>
public static class RoundWeek
{
    /// <summary>The other rounds sharing the round's number that still count (not cancelled).</summary>
    public static IQueryable<Round> Siblings(AppDbContext db, Round round) =>
        db.Rounds.Where(r => r.SeasonId == round.SeasonId
            && r.Number == round.Number
            && r.Id != round.Id
            && r.Status != RoundStatus.Cancelled);

    /// <summary>
    /// True when no sibling comes after the round: scoring it decides who missed the whole
    /// round. Always true for a standalone round.
    /// </summary>
    public static bool Decides(Round round, IEnumerable<Round> siblings) =>
        siblings.All(s => s.Part < round.Part);

    /// <summary>
    /// Nobody can submit predictions any more: the round is Locked/Scored, or its general
    /// deadline has passed (the same test the temporary standings use for "absent").
    /// </summary>
    public static bool IsClosedForPredictions(Round round, DateTime now) =>
        round.Status is RoundStatus.Locked or RoundStatus.Scored
        || (round.PredictionDeadlineUtc is { } deadline && now > deadline);

    /// <summary>A part still open to predictions blocks finalizing the part that decides.</summary>
    public static bool IsOpen(Round round) => round.Status is RoundStatus.Draft or RoundStatus.Published;
}
