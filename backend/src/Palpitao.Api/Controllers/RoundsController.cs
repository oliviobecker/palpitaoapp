using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Standings;
using Sentry;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("rounds")]
[Authorize]
[RequireGroupParticipant]
public class RoundsController : ControllerBase
{
    private readonly IRoundService _rounds;
    private readonly IRoundScoringService _scoring;
    private readonly ITemporaryStandingsService _temporaryStandings;

    public RoundsController(
        IRoundService rounds,
        IRoundScoringService scoring,
        ITemporaryStandingsService temporaryStandings)
    {
        _rounds = rounds;
        _scoring = scoring;
        _temporaryStandings = temporaryStandings;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoundSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _rounds.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoundDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _rounds.GetByIdAsync(id, ct));

    [HttpPost]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundDto>> Create(CreateRoundRequest request, CancellationToken ct)
    {
        var round = await _rounds.CreateAsync(request, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Round created.", "rounds", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["number"] = round.Number.ToString(),
        });
        return CreatedAtAction(nameof(GetById), new { id = round.Id }, round);
    }

    [HttpPut("{id:guid}")]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundDto>> Update(Guid id, UpdateRoundRequest request, CancellationToken ct)
        => Ok(await _rounds.UpdateAsync(id, request, User.GetUserId(), ct));

    [HttpPost("{id:guid}/publish")]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundDto>> Publish(Guid id, CancellationToken ct)
    {
        var round = await _rounds.PublishAsync(id, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Round published.", "rounds", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["number"] = round.Number.ToString(),
        });
        return Ok(round);
    }

    [HttpPost("{id:guid}/lock")]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundDto>> Lock(Guid id, CancellationToken ct)
    {
        var round = await _rounds.LockAsync(id, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Round locked.", "rounds", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["number"] = round.Number.ToString(),
        });
        return Ok(round);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _rounds.CancelAsync(id, User.GetUserId(), ct));

    [HttpPost("{id:guid}/reopen")]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundDto>> Reopen(Guid id, CancellationToken ct)
        => Ok(await _rounds.ReopenAsync(id, User.GetUserId(), ct));

    [HttpPost("{roundId:guid}/matches")]
    [RequireGroupAdmin]
    public async Task<ActionResult<MatchDto>> AddMatch(Guid roundId, CreateMatchRequest request, CancellationToken ct)
    {
        var match = await _rounds.AddMatchAsync(roundId, request, User.GetUserId(), ct);
        return Created($"/matches/{match.Id}", match);
    }

    [HttpPost("{roundId:guid}/score")]
    [RequireGroupAdmin]
    public async Task<ActionResult<RoundResultsDto>> Score(Guid roundId, CancellationToken ct)
    {
        var results = await _scoring.ScoreRoundAsync(roundId, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("Round scoring calculated.", "scoring", data: new Dictionary<string, string>
        {
            ["roundId"] = roundId.ToString(),
        });
        return Ok(results);
    }

    [HttpGet("{roundId:guid}/results")]
    public async Task<ActionResult<RoundResultsDto>> Results(Guid roundId, CancellationToken ct)
        => Ok(await _scoring.GetRoundResultsAsync(roundId, ct));

    /// <summary>Temporary (in-progress) standings of the round; not the official result.</summary>
    [HttpGet("{roundId:guid}/temporary-standings")]
    public async Task<ActionResult<TemporaryStandingsDto>> TemporaryStandings(Guid roundId, CancellationToken ct)
        => Ok(await _temporaryStandings.GetTemporaryStandingsAsync(roundId, ct));
}
