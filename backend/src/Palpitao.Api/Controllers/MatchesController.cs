using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Application.Rounds;
using Palpitao.Application.Scoring;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("matches")]
[Authorize]
[RequireGroupAdmin]
public class MatchesController : ControllerBase
{
    private readonly IRoundService _rounds;
    private readonly IRoundScoringService _scoring;

    public MatchesController(IRoundService rounds, IRoundScoringService scoring)
    {
        _rounds = rounds;
        _scoring = scoring;
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MatchDto>> Update(Guid id, UpdateMatchRequest request, CancellationToken ct)
        => Ok(await _rounds.UpdateMatchAsync(id, request, User.GetUserId(), ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? justification, CancellationToken ct)
    {
        await _rounds.DeleteMatchAsync(id, justification, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/result")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetResult(Guid id, MatchResultRequest request, CancellationToken ct)
    {
        await _scoring.SetMatchResultAsync(id, request, User.GetUserId(), ct);
        return NoContent();
    }
}
