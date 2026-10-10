using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Domain.Rounds;

/// <summary>What undoing a cancellation needs to know about a round.</summary>
public static class RoundLifecycle
{
    /// <summary>
    /// The status a cancelled round goes back to: the one it was cancelled from. Cancelling keeps
    /// both timestamps, unlocking clears <see cref="Round.LockedAt"/> and a Scored round cannot be
    /// cancelled, so the timestamps tell Draft, Published and Locked apart.
    /// </summary>
    public static RoundStatus RestoreTarget(Round round) =>
        round.LockedAt is not null ? RoundStatus.Locked
        : round.PublishedAt is not null ? RoundStatus.Published
        : RoundStatus.Draft;
}
