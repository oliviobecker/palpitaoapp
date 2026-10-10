using Palpitao.Domain.Rounds;

namespace Palpitao.Application.Rounds;

/// <summary>
/// Groups the rounds of a week into one round played in parts ("10.1", "10.2") and back. Each
/// operation renumbers what it has to and, when a Scored round is affected, replays the season in
/// the same transaction — the absences of a round played in parts are decided by its last part,
/// so moving parts around changes them.
/// </summary>
public interface IRoundWeekService
{
    /// <summary>Creates the round and plays it as the next part of the previous round.</summary>
    Task<RoundDto> CreateInPreviousWeekAsync(CreateRoundRequest request, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Plays a standalone round as the next part of the previous round ("11" → "10.2"), closing
    /// the gap it leaves in the numbering. Works on Scored rounds too.
    /// </summary>
    Task<RoundDto> JoinPreviousWeekAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Takes the last part out of its round played in parts ("10.2" → "11"), making room for it
    /// in the numbering. Works on Scored rounds too.
    /// </summary>
    Task<RoundDto> LeaveWeekAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Cancels the round; when it is a part and another part is already Scored, the season is
    /// replayed, since the part that decides the absences may have changed.
    /// </summary>
    Task<RoundDto> CancelAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Undoes a cancellation (back to the status it was cancelled from); like the cancel, the
    /// season is replayed when another part is already Scored.
    /// </summary>
    Task<RoundDto> RestoreAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Deletes a Draft or Cancelled round for good — matches, predictions, OCR imports and any
    /// results it still holds — and closes the gap it leaves in the numbering. The season is
    /// replayed when a Scored round moves or shares its number, or when it still held results.
    /// </summary>
    Task<RoundDeletion> DeleteAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
}

/// <summary>What a round deletion did: the slot it freed, how many rounds moved, whether it replayed.</summary>
public sealed record RoundDeletion(int Number, int Part, int Renumbered, bool Recalculated);
