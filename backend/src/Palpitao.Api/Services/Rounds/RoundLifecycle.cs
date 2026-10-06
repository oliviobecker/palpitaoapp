using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Rounds;

/// <summary>What undoing a cancellation and deleting a round need to know about a round.</summary>
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

    /// <summary>
    /// The round still holds scoring output — a round reopened and then cancelled keeps it. The
    /// standings sum those results whatever the round's status, and its absences may have
    /// eliminated someone, so removing them takes a season replay.
    /// </summary>
    public static async Task<bool> HasScoringRowsAsync(AppDbContext db, Guid roundId, CancellationToken ct) =>
        await db.RoundParticipantResults.AnyAsync(x => x.RoundId == roundId, ct)
        || await db.PredictionScores.AnyAsync(x => x.RoundId == roundId, ct)
        || await db.Absences.AnyAsync(x => x.RoundId == roundId, ct);
}
