using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Application.Teams;
using Palpitao.Domain.Enums;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
[RequireGroupParticipant]
public class TeamsController : ControllerBase
{
    private readonly ITeamCatalogService _teams;

    public TeamsController(ITeamCatalogService teams)
    {
        _teams = teams;
    }

    /// <summary>
    /// Lists clubs ordered by name. When <paramref name="competition"/> is a
    /// tracked league division (Premier League, Championship or League One),
    /// only clubs playing in that division are returned. The FA Cup (and any
    /// unrecognised value) returns every club, since cups draw from all
    /// divisions.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> List([FromQuery] Competition? competition, CancellationToken ct)
        => Ok(await _teams.ListAsync(competition, ct));
}
