using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Public;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Services.Standings;

namespace Palpitao.Api.Controllers;

/// <summary>
/// Public, unauthenticated standings and scoring audit, addressed by a season's public key.
/// </summary>
/// <remarks>
/// This lives in its own controller on purpose. <see cref="RequireGroupParticipantAttribute"/>
/// and <see cref="RequireGroupAdminAttribute"/> are action filters, not authorization filters,
/// so <c>[AllowAnonymous]</c> on a method of an existing controller would switch off
/// <c>[Authorize]</c> while the group filter still ran and rejected the caller with 403.
/// </remarks>
[ApiController]
[Route("public/seasons")]
[AllowAnonymous]
[IgnoreRequestGroup]
[EnableRateLimiting("public")]
public class PublicStandingsController : ControllerBase
{
    private readonly IPublicStandingsService _public;

    public PublicStandingsController(IPublicStandingsService publicStandings)
    {
        _public = publicStandings;
    }

    /// <summary>Season identity, the rounds visible on the link, and the ruleset behind them.</summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<PublicSeasonDto>> Season(string key, CancellationToken ct)
    {
        NoIndex();
        return Ok(await _public.GetSeasonAsync(key, ct));
    }

    /// <summary>The season's official accumulated standings, each row with its round history.</summary>
    [HttpGet("{key}/standings")]
    public async Task<ActionResult<IReadOnlyList<PublicStandingRowDto>>> Standings(string key, CancellationToken ct)
    {
        NoIndex();
        return Ok(await _public.GetStandingsAsync(key, ct));
    }

    /// <summary>One round's per-participant, per-match scoring breakdown.</summary>
    [HttpGet("{key}/rounds/{number:int}")]
    public async Task<ActionResult<PublicRoundDto>> Round(string key, int number, CancellationToken ct)
    {
        NoIndex();
        return Ok(await _public.GetRoundAsync(key, number, ct));
    }

    /// <summary>
    /// Keeps the page out of search results: it carries participants' names, and the link is
    /// meant to be shared by whoever holds the key — not found by strangers searching a name.
    /// </summary>
    private void NoIndex() => Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
}
