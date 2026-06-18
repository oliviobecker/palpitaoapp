using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.Services.Absences;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
[RequireGroupAdmin]
public class AdminAbsencesController : ControllerBase
{
    private readonly IAbsenceService _absences;

    public AdminAbsencesController(IAbsenceService absences)
    {
        _absences = absences;
    }

    [HttpGet("users/{userId:guid}/absences")]
    public async Task<ActionResult<IReadOnlyList<AbsenceDto>>> GetUserAbsences(Guid userId, CancellationToken ct)
        => Ok(await _absences.GetUserAbsencesAsync(userId, ct));

    [HttpGet("rounds/{roundId:guid}/absences")]
    public async Task<ActionResult<IReadOnlyList<AbsenceDto>>> GetRoundAbsences(Guid roundId, CancellationToken ct)
        => Ok(await _absences.GetRoundAbsencesAsync(roundId, ct));

    [HttpPost("rounds/{roundId:guid}/absences/override")]
    public async Task<IActionResult> Override(Guid roundId, AbsenceOverrideRequest request, CancellationToken ct)
    {
        await _absences.ApplyOverrideAsync(roundId, request, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid userId, ReactivateRequest request, CancellationToken ct)
    {
        await _absences.ReactivateAsync(userId, request.Justification, User.GetUserId(), ct);
        return NoContent();
    }
}
