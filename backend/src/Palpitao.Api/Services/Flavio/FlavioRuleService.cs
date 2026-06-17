using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Tournaments;

namespace Palpitao.Api.Services.Flavio;

public class FlavioRuleService : IFlavioRuleService
{
    public const int FirstApplicableRound = 16;
    private const int StandardWindowHours = 24;
    private const int ShortWindowHours = 12;

    private readonly AppDbContext _db;

    public FlavioRuleService(AppDbContext db)
    {
        _db = db;
    }

    public bool AppliesToRound(int roundNumber) => roundNumber >= FirstApplicableRound;

    public bool ShouldApplyEnglandFlavioRule(Round round) => AppliesToRound(round.Number);

    public bool ShouldApplyWorldCupFlavioRule(Round round)
        => round.Matches.Any(m => TournamentRules.IsWorldCupFlavioPhase(m.Phase));

    public bool ShouldApplyFlavioRule(Round round, TournamentType type) => type switch
    {
        TournamentType.FifaWorldCup => ShouldApplyWorldCupFlavioRule(round),
        _ => ShouldApplyEnglandFlavioRule(round),
    };

    public FlavioDeadline ComputeSpecialDeadline(Round round)
    {
        var reference = round.MirrorPublishedAt ?? round.PublishedAt;
        if (reference is null || round.FirstMatchStartsAt is null)
        {
            throw new BusinessRuleException("flavio.insufficientData");
        }

        var firstMatch = round.FirstMatchStartsAt.Value;

        // 12h window when the round was published less than 24h before the first match.
        var publishedShortNotice = (firstMatch - reference.Value) < TimeSpan.FromHours(StandardWindowHours);
        var windowHours = publishedShortNotice ? ShortWindowHours : StandardWindowHours;

        var raw = reference.Value.AddHours(windowHours);
        var conflict = raw > firstMatch;
        var effective = conflict ? firstMatch : raw;

        return new FlavioDeadline(reference.Value, windowHours, raw, effective, conflict);
    }

    /// <summary>
    /// Effective special deadline, or null when the round lacks the data to compute
    /// it (not published / no first match yet). Same rule as
    /// <see cref="ComputeSpecialDeadline"/>, but non-throwing — for display.
    /// </summary>
    public static DateTime? TryComputeEffectiveDeadline(Round round)
    {
        var reference = round.MirrorPublishedAt ?? round.PublishedAt;
        if (reference is null || round.FirstMatchStartsAt is null)
        {
            return null;
        }

        var firstMatch = round.FirstMatchStartsAt.Value;
        var shortNotice = (firstMatch - reference.Value) < TimeSpan.FromHours(StandardWindowHours);
        var raw = reference.Value.AddHours(shortNotice ? ShortWindowHours : StandardWindowHours);
        return raw > firstMatch ? firstMatch : raw;
    }

    public int ApplyHalfPenalty(int grossPoints) => (int)Math.Floor(grossPoints / 2.0);

    public async Task<IReadOnlyList<Guid>> GetLeadersBeforeRoundAsync(Guid seasonId, CancellationToken ct)
    {
        var standings = await _db.Standings
            .Where(s => s.SeasonId == seasonId)
            .ToListAsync(ct);

        if (standings.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var topPoints = standings.Max(s => s.TotalPoints);

        // All participants tied at the top are leaders.
        return standings
            .Where(s => s.TotalPoints == topPoints)
            .Select(s => s.UserId)
            .ToList();
    }

    public async Task<bool> ShouldPenalizeLeaderAsync(Guid roundId, Guid leaderUserId, CancellationToken ct)
    {
        var round = await _db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw new NotFoundException("notFound.round");

        var type = await _db.Groups
            .Where(g => g.Id == round.GroupId)
            .Select(g => g.TournamentType)
            .FirstAsync(ct);

        if (!ShouldApplyFlavioRule(round, type))
        {
            return false;
        }

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.UserId == leaderUserId)
            .ToListAsync(ct);

        // Incomplete / no predictions -> treated as a normal absence, not Flávio.
        if (predictions.Count < round.Matches.Count)
        {
            return false;
        }

        var completedAt = predictions.Max(p => p.SubmittedAt);
        var deadline = ComputeSpecialDeadline(round).EffectiveDeadlineUtc;

        return completedAt > deadline;
    }
}
