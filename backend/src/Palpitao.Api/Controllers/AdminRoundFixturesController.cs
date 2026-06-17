using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Services.Fixtures;
using Sentry;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/rounds")]
[Authorize]
[RequireGroupAdmin]
public class AdminRoundFixturesController : ControllerBase
{
    private readonly IFixtureImportService _fixtures;

    public AdminRoundFixturesController(IFixtureImportService fixtures)
    {
        _fixtures = fixtures;
    }

    /// <summary>Imports the selected fixtures into an existing round as matches.</summary>
    [HttpPost("{roundId:guid}/matches/import")]
    public async Task<ActionResult<ImportFixturesResponse>> Import(
        Guid roundId, ImportFixturesRequest request, CancellationToken ct)
    {
        var response = await _fixtures.ImportAsync(roundId, request, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Fixtures imported.", "fixtures", data: new Dictionary<string, string>
        {
            ["roundId"] = roundId.ToString(),
            ["imported"] = response.ImportedCount.ToString(),
        });
        return Ok(response);
    }
}
