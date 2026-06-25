using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.DTOs.Seasons;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Seasons;
using Palpitao.Api.Services.Standings;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("seasons")]
[Authorize]
[RequireGroupParticipant]
public class SeasonsController : ControllerBase
{
    private readonly IStandingsService _standings;
    private readonly IRoundScoringService _scoring;
    private readonly ISeasonService _seasons;
    private readonly ISeasonScoringConfigService _scoringConfig;

    public SeasonsController(
        IStandingsService standings,
        IRoundScoringService scoring,
        ISeasonService seasons,
        ISeasonScoringConfigService scoringConfig)
    {
        _standings = standings;
        _scoring = scoring;
        _seasons = seasons;
        _scoringConfig = scoringConfig;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonDto>>> List(CancellationToken ct)
        => Ok(await _seasons.ListAsync(ct));

    [HttpGet("active")]
    public async Task<ActionResult<SeasonDto>> Active(CancellationToken ct)
    {
        var season = await _seasons.GetActiveAsync(ct);
        return season is null ? NoContent() : Ok(season);
    }

    [HttpPost]
    [RequireGroupAdmin]
    public async Task<ActionResult<SeasonDto>> Create(SeasonRequest request, CancellationToken ct)
        => Ok(await _seasons.CreateAsync(request, User.GetUserId(), ct));

    [HttpPut("{id:guid}")]
    [RequireGroupAdmin]
    public async Task<ActionResult<SeasonDto>> Update(Guid id, SeasonRequest request, CancellationToken ct)
        => Ok(await _seasons.UpdateAsync(id, request, User.GetUserId(), ct));

    [HttpPost("{id:guid}/activate")]
    [RequireGroupAdmin]
    public async Task<ActionResult<SeasonDto>> Activate(Guid id, CancellationToken ct)
        => Ok(await _seasons.SetActiveAsync(id, User.GetUserId(), ct));

    [HttpGet("{seasonId:guid}/standings")]
    public async Task<ActionResult<IReadOnlyList<StandingDto>>> Standings(Guid seasonId, CancellationToken ct)
        => Ok(await _standings.GetStandingsAsync(seasonId, ct));

    [HttpPost("{seasonId:guid}/recalculate")]
    [RequireGroupAdmin]
    public async Task<IActionResult> Recalculate(Guid seasonId, CancellationToken ct)
    {
        await _scoring.RecalculateSeasonAsync(seasonId, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("{seasonId:guid}/scoring-config")]
    public async Task<ActionResult<ScoringConfigDto>> ScoringConfig(Guid seasonId, CancellationToken ct)
        => Ok(await _scoringConfig.GetConfigAsync(seasonId, ct));

    [HttpPut("{seasonId:guid}/scoring-config")]
    [RequireGroupAdmin]
    public async Task<ActionResult<ScoringConfigDto>> UpdateScoringConfig(
        Guid seasonId, ScoringConfigRequest request, CancellationToken ct)
        => Ok(await _scoringConfig.UpdateAsync(seasonId, request, User.GetUserId(), ct));
}
