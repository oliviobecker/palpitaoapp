using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Teams;

/// <summary>
/// Reads the current squad list of a competition from an external source, so the
/// global team catalogue can be reviewed against reality instead of being fixed by
/// hand-written migrations. Names only — the catalogue has no external ids, so
/// matching goes through <c>FootballReference.Canonical</c>.
/// </summary>
public interface ITeamCatalogProvider
{
    /// <summary>Human-readable source name, surfaced in the sync response and audit.</summary>
    string SourceName { get; }

    /// <summary>
    /// Distinct team names taking part in the competition this season. Returns an
    /// empty list when the source has no data for it (unsupported competition, or
    /// off-season) — callers must treat "empty" as "unknown", never as "no teams".
    /// </summary>
    Task<IReadOnlyList<string>> GetTeamNamesAsync(Competition competition, CancellationToken cancellationToken);
}
