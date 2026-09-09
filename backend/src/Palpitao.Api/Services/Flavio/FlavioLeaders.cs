using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Flavio;

public static class FlavioLeaders
{
    /// <summary>Historical net totals, never the current standings cache or future rounds.</summary>
    public static async Task<IReadOnlyList<Guid>> GetBeforeRoundAsync(
        AppDbContext db, Guid roundId, CancellationToken ct)
    {
        var round = await db.Rounds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw new NotFoundException("notFound.round");
        var totals = await db.RoundParticipantResults.AsNoTracking()
            .Where(r => r.GroupId == round.GroupId && r.SeasonId == round.SeasonId
                && db.Rounds.Any(prior => prior.Id == r.RoundId
                    && prior.Number < round.Number && prior.Status != RoundStatus.Cancelled))
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(r => r.FinalPoints - r.PenaltyPoints) })
            .ToListAsync(ct);
        if (totals.Count == 0) return Array.Empty<Guid>();
        var top = totals.Max(t => t.Points);
        return totals.Where(t => t.Points == top).Select(t => t.UserId).ToList();
    }
}
