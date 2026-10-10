using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Infrastructure.Identity;
using Palpitao.Application.Auth;
using Palpitao.Application.AdminPredictions;
using Palpitao.Application.Audit;
using Palpitao.Application.Flavio;
using Palpitao.Application.Ocr;
using Palpitao.Application.Registrations;
using Palpitao.Application.Users;

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
