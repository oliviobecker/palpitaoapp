using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.Services.Predictions;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("rounds/{roundId:guid}")]
[Authorize]
[RequireGroupParticipant]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionsService _predictions;

    public PredictionsController(IPredictionsService predictions)
    {
        _predictions = predictions;
    }

    /// <summary>Predictions of the authenticated participant for the round.</summary>
    [HttpGet("predictions/me")]
    public async Task<ActionResult<MyPredictionsDto>> GetMine(Guid roundId, CancellationToken ct)
        => Ok(await _predictions.GetMyPredictionsAsync(roundId, User.GetUserId(), ct));

    /// <summary>Creates the participant's predictions for the whole round.</summary>
    [HttpPost("predictions")]
    public async Task<ActionResult<MyPredictionsDto>> Create(Guid roundId, SavePredictionsRequest request, CancellationToken ct)
        => Ok(await _predictions.SavePredictionsAsync(roundId, User.GetUserId(), request, isEdit: false, ct));

    /// <summary>Edits the participant's predictions for the whole round.</summary>
    [HttpPut("predictions")]
    public async Task<ActionResult<MyPredictionsDto>> Update(Guid roundId, SavePredictionsRequest request, CancellationToken ct)
        => Ok(await _predictions.SavePredictionsAsync(roundId, User.GetUserId(), request, isEdit: true, ct));

    /// <summary>Predictions mirror — released only after the round is locked.</summary>
    [HttpGet("mirror")]
    public async Task<ActionResult<MirrorDto>> GetMirror(Guid roundId, CancellationToken ct)
        => Ok(await _predictions.GetMirrorAsync(roundId, User.GetUserId(), ct));
}
