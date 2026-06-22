using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Services.Localization;
using Palpitao.Api.Services.Results;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/rounds")]
[Authorize]
[RequireGroupAdmin]
public class AdminResultsController : ControllerBase
{
    private readonly IResultsUpdateService _results;
    private readonly ILocalizationService _localizer;

    public AdminResultsController(IResultsUpdateService results, ILocalizationService localizer)
    {
        _results = results;
        _localizer = localizer;
    }

    /// <summary>
    /// Refreshes the round's results from the configured provider and recomputes the
    /// temporary standings. Does NOT close the round (status stays as-is).
    /// </summary>
    [HttpPost("{roundId:guid}/refresh-results")]
    public async Task<ActionResult<RefreshResultsResponse>> Refresh(Guid roundId, CancellationToken ct)
    {
        var response = await _results.RefreshAsync(roundId, User.GetUserId(), ct);
        response.Message = _localizer.Get(response.ProviderEnabled
            ? "results.refreshed"
            : "results.providerDisabled");
        return Ok(response);
    }
}
