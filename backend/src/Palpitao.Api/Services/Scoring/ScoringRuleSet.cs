using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Immutable, resolved scoring ruleset consumed by the scoring engine. Built either from a
/// persisted <see cref="Entities.SeasonScoringConfig"/> or from the tournament-type
/// defaults (<see cref="ScoringDefaults"/>). Pure lookups — no DB access — so it is safe to
/// reuse across a whole round/season scoring pass.
/// </summary>
public sealed class ScoringRuleSet
{
    private readonly IReadOnlyDictionary<ScoreCategory, int> _basePoints;
    private readonly IReadOnlyDictionary<(int Low, int High), ScoreCategory> _scoreCategories;
    private readonly IReadOnlyDictionary<(Competition Competition, MatchPhase Phase), (int Normal, int Classic)> _multipliers;

    /// <summary>Teams that count as classic-eligible (Big Seven / world champions) this season.</summary>
    public IReadOnlySet<Guid> ClassicTeamIds { get; }

    public ScoringRuleSet(
        IReadOnlyDictionary<ScoreCategory, int> basePoints,
        IReadOnlyDictionary<(int, int), ScoreCategory> scoreCategories,
        IReadOnlyDictionary<(Competition, MatchPhase), (int, int)> multipliers,
        IReadOnlySet<Guid> classicTeamIds)
    {
        _basePoints = basePoints;
        _scoreCategories = scoreCategories;
        _multipliers = multipliers;
        ClassicTeamIds = classicTeamIds;
    }

    /// <summary>Base points awarded for a category (before the multiplier).</summary>
    public int BasePointsFor(ScoreCategory category)
        => _basePoints.TryGetValue(category, out var pts) ? pts : 0;

    /// <summary>
    /// Difficulty category of an exact score (Traditional / Medium / Uncommon); any score
    /// not explicitly mapped is <see cref="ScoreCategory.ExtraUncommon"/>.
    /// </summary>
    public ScoreCategory CategoryForExactScore(int home, int away)
    {
        var key = (Math.Min(home, away), Math.Max(home, away));
        return _scoreCategories.TryGetValue(key, out var category) ? category : ScoreCategory.ExtraUncommon;
    }

    /// <summary>True when the team is classic-eligible this season.</summary>
    public bool IsClassicTeam(Guid teamId) => ClassicTeamIds.Contains(teamId);

    /// <summary>
    /// Effective multiplier for a (competition, phase): the classic value when
    /// <paramref name="isClassic"/>, otherwise the normal value. Falls back to the
    /// competition's <see cref="MatchPhase.Regular"/> row when the exact phase is absent
    /// (covers e.g. League One matches whose phase is not modelled), then to 1.
    /// </summary>
    public int MultiplierFor(Competition competition, MatchPhase phase, bool isClassic)
    {
        if (_multipliers.TryGetValue((competition, phase), out var rule))
        {
            return isClassic ? rule.Classic : rule.Normal;
        }

        if (_multipliers.TryGetValue((competition, MatchPhase.Regular), out var fallback))
        {
            return isClassic ? fallback.Classic : fallback.Normal;
        }

        return 1;
    }
}
