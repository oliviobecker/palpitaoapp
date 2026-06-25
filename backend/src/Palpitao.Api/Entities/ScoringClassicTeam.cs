namespace Palpitao.Api.Entities;

/// <summary>
/// A team designated as "classic-eligible" for the season (a Big Seven club for the
/// England certame / a world champion for the World Cup). Snapshotted per season instead
/// of read from the global (shared) <see cref="Team"/> catalogue, so a group designating
/// its own classics never affects another tenant. A match counts as a classic when both
/// its teams appear here.
/// </summary>
public class ScoringClassicTeam
{
    public Guid Id { get; set; }

    public Guid ConfigId { get; set; }

    public Guid TeamId { get; set; }

    // Navigation
    public SeasonScoringConfig? Config { get; set; }
    public Team? Team { get; set; }
}
