using Palpitao.Api.DTOs.Public;
using Palpitao.Api.DTOs.Scoring;

namespace Palpitao.Api.Services.Standings;

/// <summary>
/// Read-only standings and scoring audit for the public, key-addressed link.
/// </summary>
/// <remarks>
/// Deliberately does <b>not</b> depend on <see cref="Groups.ICurrentGroupService"/>: there is
/// no session and no <c>X-Group-Id</c> to validate. The season public key is the entire
/// credential, and the tenant is derived from the season it resolves to. Because the EF
/// global filter is inert without a request group — it matches every group rather than none —
/// every query here scopes itself explicitly instead of relying on it.
///
/// Only closed rounds (<c>Locked</c>/<c>Scored</c>) are ever exposed, so the link cannot leak
/// a prediction while it could still be copied.
/// </remarks>
public interface IPublicStandingsService
{
    /// <summary>Season identity, its visible rounds and the ruleset behind the numbers.</summary>
    Task<PublicSeasonDto> GetSeasonAsync(string key, CancellationToken ct);

    /// <summary>The season's official accumulated standings, each row with its round history.</summary>
    Task<IReadOnlyList<PublicStandingRowDto>> GetStandingsAsync(string key, CancellationToken ct);

    /// <summary>One round's per-participant, per-match scoring breakdown.</summary>
    Task<PublicRoundDto> GetRoundAsync(string key, int roundNumber, CancellationToken ct);
}
