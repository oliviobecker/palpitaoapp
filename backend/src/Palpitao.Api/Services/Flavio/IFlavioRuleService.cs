using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Flavio;

/// <summary>Computed special deadline for the leader(s) under the Flávio rule.</summary>
public record FlavioDeadline(
    DateTime ReferenceUtc,
    int WindowHours,
    DateTime RawDeadlineUtc,
    DateTime EffectiveDeadlineUtc,
    bool Conflict);

public interface IFlavioRuleService
{
    /// <summary>
    /// England rule: the Flávio rule only applies from <paramref name="firstApplicableRound"/>
    /// on — the season's <c>FlavioFromRound</c> setting (default 16). Passed explicitly rather
    /// than defaulted so a configured threshold can never be silently ignored.
    /// </summary>
    bool AppliesToRound(int roundNumber, int firstApplicableRound);

    /// <summary>England rule applicability (round number ≥ the season's threshold).</summary>
    bool ShouldApplyEnglandFlavioRule(Round round, int firstApplicableRound);

    /// <summary>World Cup rule applicability: the round has at least one match from
    /// the quarter-finals on. Requires <c>round.Matches</c> to be loaded. The round-number
    /// threshold does not apply to this variant.</summary>
    bool ShouldApplyWorldCupFlavioRule(Round round);

    /// <summary>Applicability for the given certame type (dispatches England/World Cup).</summary>
    bool ShouldApplyFlavioRule(Round round, TournamentType type, int firstApplicableRound);

    /// <summary>
    /// Computes the leader's special deadline. Reference = MirrorPublishedAt
    /// (fallback PublishedAt); window = 12h when the round was published less than
    /// 24h before the first match, otherwise 24h; the general lock always prevails.
    /// </summary>
    FlavioDeadline ComputeSpecialDeadline(Round round);

    /// <summary>Halves the round points, rounding down (17 -> 8).</summary>
    int ApplyHalfPenalty(int grossPoints);

    /// <summary>Leader(s) of the season's general standing before the round.</summary>
    Task<IReadOnlyList<Guid>> GetLeadersBeforeRoundAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// True when the leader submitted a complete set of predictions after the
    /// special deadline (but before the general lock). A leader who did not
    /// predict (or predicted incompletely) is handled as a normal absence.
    /// </summary>
    Task<bool> ShouldPenalizeLeaderAsync(
        Guid roundId, Guid leaderUserId, int firstApplicableRound, CancellationToken ct);
}
