using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Scouts;
using Palpitao.Api.Services.Scouts;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/rounds/{roundId:guid}/scout")]
[Authorize]
[RequireGroupAdmin]
public class AdminScoutController : ControllerBase
{
    private readonly IScoutService _scout;

    public AdminScoutController(IScoutService scout)
    {
        _scout = scout;
    }

    /// <summary>
    /// Scout of the round: participants grouped by the exact scoreline they
    /// predicted, per match. Drives the copy-ready group "Scout" message.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RoundScoutDto>> Get(Guid roundId, CancellationToken ct)
        => Ok(await _scout.GetRoundScoutAsync(roundId, ct));
}
