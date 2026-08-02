using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Teams;
using Palpitao.Api.Services.Teams;
using Sentry;

namespace Palpitao.Api.Controllers;

/// <summary>
/// Admin maintenance of the global team catalogue. The catalogue is shared by every
/// group, so these writes are cross-tenant by nature — the same is already true of
/// the teams fixture import creates on the fly.
/// </summary>
[ApiController]
[Route("admin/teams")]
[Authorize]
[RequireGroupAdmin]
public class AdminTeamsController : ControllerBase
{
    private readonly ITeamCatalogService _catalog;

    public AdminTeamsController(ITeamCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Moves a club to another league division (null clears it).</summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AdminTeamDto>> UpdateDivision(
        Guid id, UpdateTeamDivisionRequest request, CancellationToken ct)
        => Ok(await _catalog.UpdateDivisionAsync(id, request, User.GetUserId(), ct));

    /// <summary>Compares the catalogue with the external squad lists without writing anything.</summary>
    [HttpPost("sync-preview")]
    public async Task<ActionResult<TeamSyncResponse>> SyncPreview(CancellationToken ct)
        => Ok(await _catalog.SyncAsync(apply: false, User.GetUserId(), ct));

    /// <summary>Recomputes the comparison and saves it.</summary>
    [HttpPost("sync-apply")]
    public async Task<ActionResult<TeamSyncResponse>> SyncApply(CancellationToken ct)
    {
        var response = await _catalog.SyncAsync(apply: true, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Team catalogue synced.", "teams", data: new Dictionary<string, string>
        {
            ["source"] = response.Source,
            ["created"] = response.Competitions.Sum(c => c.ToCreate.Count).ToString(),
            ["moved"] = response.Competitions.Sum(c => c.ToMove.Count).ToString(),
        });
        return Ok(response);
    }
}
