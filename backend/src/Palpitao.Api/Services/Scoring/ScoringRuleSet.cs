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

    /// <summary>
    /// Classic-eligible teams of the season, each mapped to the classic group it belongs to
    /// (Big Seven → Premier League, the Championship rivals → Championship, world champions →
    /// World Cup).
    /// </summary>
    public IReadOnlyDictionary<Guid, Competition> ClassicTeams { get; }

    /// <summary>The season's Flávio Rule / absence-punishment parameters.</summary>
    public SeasonRuleParams Rules { get; }

    public ScoringRuleSet(
        IReadOnlyDictionary<ScoreCategory, int> basePoints,
        IReadOnlyDictionary<(int, int), ScoreCategory> scoreCategories,
        IReadOnlyDictionary<(Competition, MatchPhase), (int, int)> multipliers,
        IReadOnlyDictionary<Guid, Competition> classicTeams,
        SeasonRuleParams rules)
    {
        _basePoints = basePoints;
        _scoreCategories = scoreCategories;
        _multipliers = multipliers;
        ClassicTeams = classicTeams;
        Rules = rules;
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

    /// <summary>
    /// True when the two teams form a classic: both are classic-eligible <em>and</em> belong to
    /// the same group. The match's own competition is irrelevant here — it only selects the
    /// multiplier row — so a Championship pair also counts in the FA Cup, while a pair from two
    /// different groups (say a Championship rival against a Big Seven club) never does.
    /// </summary>
    public bool IsClassicPair(Guid homeTeamId, Guid awayTeamId)
        => ClassicTeams.TryGetValue(homeTeamId, out var home)
            && ClassicTeams.TryGetValue(awayTeamId, out var away)
            && home == away;

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
