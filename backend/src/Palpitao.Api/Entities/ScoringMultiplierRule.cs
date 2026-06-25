using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>
/// Multiplier for a (competition, phase) pair. <see cref="Multiplier"/> applies normally;
/// <see cref="ClassicMultiplier"/> applies when the match is a "classic" (both teams are
/// classic-eligible — see <see cref="ScoringClassicTeam"/>). Equal values mean the classic
/// flag has no effect for that row (e.g. World Cup group stage, where classics are not
/// doubled). When a match's exact (competition, phase) row is absent, the engine falls
/// back to the competition's <see cref="MatchPhase.Regular"/> row.
/// </summary>
public class ScoringMultiplierRule
{
    public Guid Id { get; set; }

    public Guid ConfigId { get; set; }

    public Competition Competition { get; set; }

    public MatchPhase Phase { get; set; }

    /// <summary>Multiplier when the match is not a classic.</summary>
    public int Multiplier { get; set; }

    /// <summary>Multiplier when both teams are classic-eligible.</summary>
    public int ClassicMultiplier { get; set; }

    // Navigation
    public SeasonScoringConfig? Config { get; set; }
}
