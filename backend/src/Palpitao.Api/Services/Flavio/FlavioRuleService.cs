using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Tournaments;

namespace Palpitao.Api.Services.Flavio;

public class FlavioRuleService : IFlavioRuleService
{
    /// <summary>
    /// Historical threshold, now only the <em>default</em> of the per-season
    /// <c>FlavioFromRound</c> setting (see <c>ScoringDefaults</c>). The applicability
    /// methods take the season's configured value explicitly.
    /// </summary>
    public const int FirstApplicableRound = 16;
    private const int StandardWindowHours = 24;
    private const int ShortWindowHours = 12;

    private readonly AppDbContext _db;

    public FlavioRuleService(AppDbContext db)
    {
        _db = db;
    }

    public bool AppliesToRound(int roundNumber, int firstApplicableRound)
        => roundNumber >= firstApplicableRound;

    public bool ShouldApplyEnglandFlavioRule(Round round, int firstApplicableRound)
        => AppliesToRound(round.Number, firstApplicableRound);

    public bool ShouldApplyWorldCupFlavioRule(Round round)
        => round.Matches.Any(m => TournamentRules.IsWorldCupFlavioPhase(m.Phase));

    public bool ShouldApplyFlavioRule(Round round, TournamentType type, int firstApplicableRound) => type switch
    {
        TournamentType.FifaWorldCup => ShouldApplyWorldCupFlavioRule(round),
        _ => ShouldApplyEnglandFlavioRule(round, firstApplicableRound),
    };

    public FlavioDeadline ComputeSpecialDeadline(Round round)
        => TryCompute(round) ?? throw new BusinessRuleException("flavio.insufficientData");

    /// <summary>
    /// The special deadline in full (reference, window, raw/effective instants and whether
    /// the general lock prevailed), or null when the round lacks the data to compute it
    /// (not published / no first match yet). Non-throwing counterpart of
    /// <see cref="ComputeSpecialDeadline"/> — for display.
    /// </summary>
    public static FlavioDeadline? TryCompute(Round round)
    {
        var reference = round.MirrorPublishedAt ?? round.PublishedAt;
        if (reference is null || round.FirstMatchStartsAt is null)
        {
            return null;
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

    /// <summary>Effective special deadline only; see <see cref="TryCompute"/>.</summary>
    public static DateTime? TryComputeEffectiveDeadline(Round round)
        => TryCompute(round)?.EffectiveDeadlineUtc;

    public int ApplyHalfPenalty(int grossPoints) => (int)Math.Floor(grossPoints / 2.0);

    public Task<IReadOnlyList<Guid>> GetLeadersBeforeRoundAsync(Guid roundId, CancellationToken ct)
        => FlavioLeaders.GetBeforeRoundAsync(_db, roundId, ct);

    public async Task<bool> ShouldPenalizeLeaderAsync(
        Guid roundId, Guid leaderUserId, int firstApplicableRound, CancellationToken ct)
    {
        var round = await _db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw new NotFoundException("notFound.round");

        var type = await _db.Seasons
            .Where(s => s.Id == round.SeasonId)
            .Select(s => s.TournamentType)
            .FirstAsync(ct);

        if (!ShouldApplyFlavioRule(round, type, firstApplicableRound))
        {
            return false;
        }

        if (await _db.FlavioOverrides.AnyAsync(o => o.RoundId == roundId
            && o.UserId == leaderUserId && o.IsExempt, ct))
            return false;

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.UserId == leaderUserId)
            .ToListAsync(ct);

        // Nothing sent -> a plain absence, which zeroes the round on its own.
        // An incomplete set is deliberately NOT a free pass. Now that an absence means
        // "sent nothing" (README §14), excusing incompleteness here would make omitting one
        // match strictly better for the leader than submitting late in full: no halving, no
        // absence, full points. The last SubmittedAt still dates a partial set.
        if (predictions.Count == 0)
        {
            return false;
        }

        var completedAt = predictions.Max(p => p.SubmittedAt);
        var deadline = ComputeSpecialDeadline(round).EffectiveDeadlineUtc;

        return completedAt > deadline;
    }
}
