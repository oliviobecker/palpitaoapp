using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Services.Fixtures;
using Sentry;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/fixtures")]
[Authorize]
[RequireGroupAdmin]
public class AdminFixturesController : ControllerBase
{
    private readonly IFixtureImportService _fixtures;

    public AdminFixturesController(IFixtureImportService fixtures)
    {
        _fixtures = fixtures;
    }

    /// <summary>Searches external fixtures available in the given period.</summary>
    [HttpPost("search")]
    public async Task<ActionResult<SearchFixturesResponse>> Search(
        SearchFixturesRequest request, CancellationToken ct)
    {
        var response = await _fixtures.SearchAsync(request, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Fixtures searched.", "fixtures", data: new Dictionary<string, string>
        {
            ["source"] = response.Source,
            ["count"] = response.Fixtures.Count.ToString(),
        });
        return Ok(response);
    }
}
