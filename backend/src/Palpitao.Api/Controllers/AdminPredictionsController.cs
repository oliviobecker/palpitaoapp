using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.AdminPredictions;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/rounds/{roundId:guid}/predictions")]
[Authorize]
[RequireGroupAdmin]
public class AdminPredictionsController : ControllerBase
{
    private readonly IAdminPredictionService _admin;

    public AdminPredictionsController(IAdminPredictionService admin)
    {
        _admin = admin;
    }

    [HttpPost("manual")]
    public async Task<IActionResult> Manual(Guid roundId, ManualPredictionRequest request, CancellationToken ct)
    {
        await _admin.SaveManualAsync(roundId, request, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("participant/{userId:guid}")]
    public async Task<ActionResult<AdminParticipantPredictionsDto>> Participant(
        Guid roundId, Guid userId, CancellationToken ct)
        => Ok(await _admin.GetParticipantPredictionsAsync(roundId, userId, ct));

    [HttpGet("coverage")]
    public async Task<ActionResult<PredictionCoverageDto>> Coverage(Guid roundId, CancellationToken ct)
        => Ok(await _admin.GetCoverageAsync(roundId, ct));
}
