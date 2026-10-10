using Palpitao.Api.DTOs.Teams;
using Palpitao.Domain.Enums;

namespace Palpitao.Api.Services.Teams;

public interface ITeamCatalogService
{
    /// <summary>
    /// Lists clubs ordered by name. A tracked league division (Premier League, Championship or
    /// League One) narrows the list to that division; the FA Cup and any other value return
    /// every club, since cups draw from all divisions.
    /// </summary>
    Task<IReadOnlyList<TeamDto>> ListAsync(Competition? competition, CancellationToken ct);

    /// <summary>Moves a club to another league division (or clears it).</summary>
    Task<AdminTeamDto> UpdateDivisionAsync(
        Guid teamId, UpdateTeamDivisionRequest request, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Compares the catalogue against the external squad lists. With
    /// <paramref name="apply"/> false nothing is written — the response is the preview
    /// the admin confirms; with it true the same comparison runs again and its result
    /// is saved, so applying is idempotent and reports what actually changed.
    /// </summary>
    Task<TeamSyncResponse> SyncAsync(bool apply, Guid actingUserId, CancellationToken ct);
}
