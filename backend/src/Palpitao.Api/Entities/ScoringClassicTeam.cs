using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>
/// A team designated as "classic-eligible" for the season (a Big Seven club for the
/// England certame / a world champion for the World Cup). Snapshotted per season instead
/// of read from the global (shared) <see cref="Team"/> catalogue, so a group designating
/// its own classics never affects another tenant. A match counts as a classic when both
/// its teams belong to the <em>same</em> classic group (<see cref="Competition"/>).
/// </summary>
public class ScoringClassicTeam
{
    public Guid Id { get; set; }

    public Guid ConfigId { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>
    /// The classic group the team belongs to (Big Seven → Premier League, the Championship
    /// rivals → Championship, world champions → World Cup). Only pairs from the same group
    /// count as a classic — the match's own competition then decides the multiplier, so a
    /// Championship pair also doubles in the FA Cup while a cross-group pair never does.
    /// </summary>
    public Competition Competition { get; set; }

    // Navigation
    public SeasonScoringConfig? Config { get; set; }
    public Team? Team { get; set; }
}
