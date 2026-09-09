using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.Flavio;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/rounds/{roundId:guid}/flavio-overrides")]
[Authorize]
[RequireGroupAdmin]
public class AdminFlavioOverridesController(FlavioOverrideService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RoundFlavioOverridesDto>> Get(Guid roundId, CancellationToken ct)
        => Ok(await service.GetAsync(roundId, ct));

    [HttpPut]
    public async Task<IActionResult> Put(Guid roundId, FlavioOverrideRequest request, CancellationToken ct)
    {
        await service.SaveAsync(roundId, request, User.GetUserId(), ct);
        return NoContent();
    }
}
