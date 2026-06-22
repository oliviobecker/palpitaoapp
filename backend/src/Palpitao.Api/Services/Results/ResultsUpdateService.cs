using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Sentry;

namespace Palpitao.Api.Services.Results;

public class ResultsUpdateService : IResultsUpdateService
{
    private readonly AppDbContext _db;
    private readonly IResultsProvider _provider;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public ResultsUpdateService(AppDbContext db, IResultsProvider provider, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _provider = provider;
        _audit = audit;
        _current = current;
    }

    public async Task<RefreshResultsResponse> RefreshAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        // The refresh never closes the round; it is only allowed while it is in play.
        switch (round.Status)
        {
            case RoundStatus.Cancelled:
                throw new BusinessRuleException("results.roundCancelled");
            case RoundStatus.Scored:
                throw new BusinessRuleException("results.roundScored");
            case RoundStatus.Draft:
                throw new BusinessRuleException("results.roundNotPublished");
        }

        var now = DateTime.UtcNow;
        var updated = await RefreshLoadedRoundAsync(round, actingUserId, now, ct);

        var finished = round.Matches.Count(m => m.Status == MatchStatus.Finished);
        var inProgress = round.Matches.Count(m => m.Status == MatchStatus.InProgress);
        var notStarted = round.Matches.Count(m => m.Status == MatchStatus.NotStarted);
        var postponed = round.Matches.Count(m => m.Status == MatchStatus.Postponed);
        var cancelled = round.Matches.Count(m => m.Status == MatchStatus.Cancelled);

        return new RefreshResultsResponse
        {
            RoundId = roundId,
            Provider = _provider.Name,
            ProviderEnabled = _provider.IsEnabled,
            UpdatedMatches = updated,
            FinishedMatches = finished,
            InProgressMatches = inProgress,
            NotStartedMatches = notStarted,
            PostponedMatches = postponed,
            CancelledMatches = cancelled,
            TemporaryStandingsUpdatedAt = now,
        };
    }

    /// <summary>
    /// Background-safe refresh of every in-play round (Published/Locked) across all
    /// groups — used by the periodic <see cref="ResultsRefreshBackgroundService"/>.
    /// Not group-scoped (no HTTP context); never closes a round. Returns the number
    /// of updated matches.
    /// </summary>
    public async Task<int> RefreshAllActiveRoundsAsync(CancellationToken ct)
    {
        var rounds = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .Include(r => r.Group)
            .Where(r => r.Status == RoundStatus.Published || r.Status == RoundStatus.Locked)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var total = 0;
        foreach (var round in rounds)
        {
            // Attribute the system refresh to the group's owner (a valid user).
            var actor = round.Group?.OwnerUserId ?? round.CreatedByUserId;
            total += await RefreshLoadedRoundAsync(round, actor, now, ct);
        }

        return total;
    }

    /// <summary>Core refresh of an already-loaded round (no group check). Fetches from
    /// the provider when enabled, applies results, stamps the round, audits and saves.</summary>
    private async Task<int> RefreshLoadedRoundAsync(Round round, Guid actingUserId, DateTime now, CancellationToken ct)
    {
        SentrySdk.AddBreadcrumb("Results refresh started.", "results", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["provider"] = _provider.Name,
            ["enabled"] = _provider.IsEnabled.ToString(),
        });

        var updated = 0;
        if (_provider.IsEnabled)
        {
            IReadOnlyList<ExternalMatchResultDto> external;
            try
            {
                external = await _provider.GetResultsForRoundAsync(round, ct);
            }
            catch (BusinessRuleException)
            {
                _audit.Add(actingUserId, "ResultsRefreshFailed", nameof(Round), round.Id.ToString(),
                    new { provider = _provider.Name });
                await _db.SaveChangesAsync(ct);
                SentrySdk.AddBreadcrumb("Results refresh failed.", "results", level: BreadcrumbLevel.Warning);
                throw;
            }

            updated = ApplyExternalResults(round, external, now);
        }

        round.ResultsUpdatedAt = now;

        var finished = round.Matches.Count(m => m.Status == MatchStatus.Finished);
        var inProgress = round.Matches.Count(m => m.Status == MatchStatus.InProgress);
        var notStarted = round.Matches.Count(m => m.Status == MatchStatus.NotStarted);

        _audit.Add(actingUserId, "ResultsRefreshed", nameof(Round), round.Id.ToString(),
            new { provider = _provider.Name, enabled = _provider.IsEnabled, updated, finished, inProgress, notStarted });
        await _db.SaveChangesAsync(ct);

        SentrySdk.AddBreadcrumb("Results refresh completed.", "results", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["updated"] = updated.ToString(),
            ["finished"] = finished.ToString(),
        });

        return updated;
    }

    /// <summary>Maps external results onto the round's matches by external id or team names.</summary>
    private static int ApplyExternalResults(Round round, IReadOnlyList<ExternalMatchResultDto> external, DateTime now)
    {
        var updated = 0;
        foreach (var result in external)
        {
            var match = FindMatch(round, result);
            if (match is null)
            {
                continue;
            }

            match.Status = result.Status;
            match.IsFinished = result.Status == MatchStatus.Finished;
            if (result.HomeScore is not null && result.AwayScore is not null)
            {
                match.HomeScore = result.HomeScore;
                match.AwayScore = result.AwayScore;
            }

            match.ResultSource = "ConfiguredWebsite";
            match.ExternalMatchId = result.ExternalMatchId ?? match.ExternalMatchId;
            match.ExternalMatchUrl = result.ExternalMatchUrl ?? match.ExternalMatchUrl;
            match.LastResultUpdatedAt = now;
            updated++;
        }

        return updated;
    }

    private static RoundMatch? FindMatch(Round round, ExternalMatchResultDto result)
    {
        if (!string.IsNullOrWhiteSpace(result.ExternalMatchId))
        {
            var byId = round.Matches.FirstOrDefault(m => m.ExternalMatchId == result.ExternalMatchId);
            if (byId is not null)
            {
                return byId;
            }
        }

        var home = FootballReference.Normalize(result.HomeTeamName);
        var away = FootballReference.Normalize(result.AwayTeamName);
        return round.Matches.FirstOrDefault(m =>
            FootballReference.Normalize(m.HomeTeam?.Name ?? string.Empty) == home
            && FootballReference.Normalize(m.AwayTeam?.Name ?? string.Empty) == away);
    }
}
