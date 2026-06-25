using Palpitao.Api.DTOs.Scoring;

namespace Palpitao.Api.Services.Scoring;

/// <summary>
/// Resolves and edits the per-season scoring ruleset. Read paths (scoring, standings)
/// build a <see cref="ScoringRuleSet"/> without persisting; the admin GET lazily
/// materialises a default config so it can be edited.
/// </summary>
public interface ISeasonScoringConfigService
{
    /// <summary>
    /// The resolved ruleset for a season: from its persisted config when present,
    /// otherwise the tournament-type defaults (seeded from the global Team flags).
    /// Never persists — safe on read/scoring paths.
    /// </summary>
    Task<ScoringRuleSet> GetRuleSetAsync(Guid seasonId, CancellationToken ct);

    /// <summary>Same as <see cref="GetRuleSetAsync"/>, resolving the season from a round.</summary>
    Task<ScoringRuleSet> GetRuleSetForRoundAsync(Guid roundId, CancellationToken ct);

    /// <summary>
    /// The editable config DTO for a season: the persisted config when present, otherwise
    /// the tournament-type defaults. Read-only — never persists (so a participant viewing
    /// the predictions page can't create admin config rows). Persisted on save via
    /// <see cref="UpdateAsync"/>.
    /// </summary>
    Task<ScoringConfigDto> GetConfigAsync(Guid seasonId, CancellationToken ct);

    /// <summary>Replaces a season's scoring ruleset (validated). Does not re-score — the admin recalculates explicitly.</summary>
    Task<ScoringConfigDto> UpdateAsync(Guid seasonId, ScoringConfigRequest request, Guid actingUserId, CancellationToken ct);
}
