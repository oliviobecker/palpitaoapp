using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Auth;
using Palpitao.Api.Data;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
[RequireGroupParticipant]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TeamsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists clubs ordered by name. When <paramref name="competition"/> is a
    /// tracked league division (Premier League, Championship or League One),
    /// only clubs playing in that division are returned. The FA Cup (and any
    /// unrecognised value) returns every club, since cups draw from all
    /// divisions.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Competition? competition, CancellationToken ct)
    {
        var query = _db.Teams.AsQueryable();

        if (competition is Competition.PremierLeague or Competition.Championship or Competition.LeagueOne)
        {
            query = query.Where(t => t.Division == competition);
        }

        var teams = await query
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.ShortName,
                t.IsBigSevenClub,
                t.CrestUrl,
                t.Division,
            })
            .ToListAsync(ct);

        return Ok(teams);
    }
}
