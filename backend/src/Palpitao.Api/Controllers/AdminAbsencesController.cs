using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Scoring;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
[RequireGroupAdmin]
public class AdminAbsencesController : ControllerBase
{
    private readonly IAbsenceService _absences;
    private readonly IRoundScoringService _scoring;

    public AdminAbsencesController(IAbsenceService absences, IRoundScoringService scoring)
    {
        _absences = absences;
        _scoring = scoring;
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

    [HttpGet("users/{userId:guid}/absence-candidates")]
    public async Task<ActionResult<IReadOnlyList<AbsenceCandidateRoundDto>>> AbsenceCandidates(
        Guid userId, CancellationToken ct)
        => Ok(await _absences.GetAbsenceCandidateRoundsAsync(userId, ct));

    [HttpPost("users/{userId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid userId, ReactivateRequest request, CancellationToken ct)
    {
        await _absences.ReactivateAsync(
            userId, request.Justification, request.AbsentRoundIds, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("users/{userId:guid}/absence-review")]
    public async Task<ActionResult<IReadOnlyList<AbsenceReviewRoundDto>>> AbsenceReview(
        Guid userId, CancellationToken ct)
        => Ok(await _absences.GetAbsenceReviewRoundsAsync(userId, ct));

    /// <summary>
    /// Applies the admin's per-round decisions and, when an already-scored round changed,
    /// recalculates the season in the same transaction.
    /// </summary>
    [HttpPost("users/{userId:guid}/absence-review")]
    public async Task<ActionResult<AbsenceReviewResultDto>> ReviewAbsences(
        Guid userId, AbsenceReviewRequest request, CancellationToken ct)
        => Ok(await _scoring.ReviewParticipantAbsencesAsync(userId, request, User.GetUserId(), ct));
}
