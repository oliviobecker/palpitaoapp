using Microsoft.EntityFrameworkCore;
using Palpitao.Application.Abstractions;

namespace Palpitao.Application.Rounds;

/// <summary>What deleting a round needs to know about the scoring output it may still hold.</summary>
public static class RoundScoringRows
{
    /// <summary>
    /// The round still holds scoring output — a round reopened and then cancelled keeps it. The
    /// standings sum those results whatever the round's status, and its absences may have
    /// eliminated someone, so removing them takes a season replay.
    /// </summary>
    public static async Task<bool> ExistAsync(IAppDbContext db, Guid roundId, CancellationToken ct) =>
        await db.RoundParticipantResults.AnyAsync(x => x.RoundId == roundId, ct)
        || await db.PredictionScores.AnyAsync(x => x.RoundId == roundId, ct)
        || await db.Absences.AnyAsync(x => x.RoundId == roundId, ct);
}
