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
        var outcome = await RefreshLoadedRoundAsync(round, actingUserId, now, ct);

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
            UpdatedMatches = outcome.Updated,
            UnmatchedMatches = outcome.Unmatched,
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
            total += (await RefreshLoadedRoundAsync(round, actor, now, ct)).Updated;
        }

        return total;
    }

    /// <summary>Core refresh of an already-loaded round (no group check). Fetches from
    /// the provider when enabled, applies results, stamps the round, audits and saves.</summary>
    private async Task<RefreshOutcome> RefreshLoadedRoundAsync(
        Round round, Guid actingUserId, DateTime now, CancellationToken ct)
    {
        SentrySdk.AddBreadcrumb("Results refresh started.", "results", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["provider"] = _provider.Name,
            ["enabled"] = _provider.IsEnabled.ToString(),
        });

        var updated = 0;
        var missing = new List<string>();
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

            (updated, var matched) = ApplyExternalResults(round, external, now);

            // A match the feed never mentioned is almost always a club the source spells its own
            // way. Name it, so the gap is visible instead of looking like "still not started".
            missing = round.Matches
                .Where(m => !matched.Contains(m.Id))
                .Select(m => $"{m.HomeTeam?.Name} x {m.AwayTeam?.Name}")
                .ToList();
        }

        round.ResultsUpdatedAt = now;

        var finished = round.Matches.Count(m => m.Status == MatchStatus.Finished);
        var inProgress = round.Matches.Count(m => m.Status == MatchStatus.InProgress);
        var notStarted = round.Matches.Count(m => m.Status == MatchStatus.NotStarted);

        _audit.Add(actingUserId, "ResultsRefreshed", nameof(Round), round.Id.ToString(),
            new
            {
                provider = _provider.Name,
                enabled = _provider.IsEnabled,
                updated,
                finished,
                inProgress,
                notStarted,
                unmatched = missing,
            });
        await _db.SaveChangesAsync(ct);

        SentrySdk.AddBreadcrumb("Results refresh completed.", "results", data: new Dictionary<string, string>
        {
            ["roundId"] = round.Id.ToString(),
            ["updated"] = updated.ToString(),
            ["finished"] = finished.ToString(),
            ["unmatched"] = missing.Count.ToString(),
        });

        return new RefreshOutcome(updated, missing.Count);
    }

    /// <summary>What one refresh did: matches whose result moved, and matches the feed did not
    /// cover at all.</summary>
    private readonly record struct RefreshOutcome(int Updated, int Unmatched);

    /// <summary>
    /// Maps external results onto the round's matches by external id or team names. Only a row
    /// that actually moves the status or the score counts as an update, so the reported number is
    /// what changed rather than how many matches the feed happened to cover.
    /// </summary>
    private (int Updated, HashSet<Guid> Matched) ApplyExternalResults(
        Round round, IReadOnlyList<ExternalMatchResultDto> external, DateTime now)
    {
        var updated = 0;
        var matched = new HashSet<Guid>();
        foreach (var result in external)
        {
            var match = FindMatch(round, result);
            if (match is null)
            {
                continue;
            }

            matched.Add(match.Id);

            // A result an admin typed in is the pool's source of truth. The feed can lag behind it
            // or join the wrong game, and the background refresh runs every five minutes.
            if (match.ResultSource == RoundMatch.ManualResultSource && match.Status == MatchStatus.Finished)
            {
                continue;
            }

            var hasScores = result.HomeScore is not null && result.AwayScore is not null;

            // A scoreless "not started" card never demotes a match we already know is being played
            // or is over: the two provider tabs disagree about upcoming fixtures.
            var demotes = !hasScores
                && result.Status == MatchStatus.NotStarted
                && match.Status is MatchStatus.InProgress or MatchStatus.Finished;

            var changed = false;
            if (!demotes && match.Status != result.Status)
            {
                match.Status = result.Status;
                match.IsFinished = result.Status == MatchStatus.Finished;
                changed = true;
            }

            if (hasScores && (match.HomeScore != result.HomeScore || match.AwayScore != result.AwayScore))
            {
                match.HomeScore = result.HomeScore;
                match.AwayScore = result.AwayScore;
                changed = true;
            }

            match.ExternalMatchId = result.ExternalMatchId ?? match.ExternalMatchId;
            match.ExternalMatchUrl = result.ExternalMatchUrl ?? match.ExternalMatchUrl;
            if (!changed)
            {
                continue;
            }

            match.ResultSource = _provider.Name;
            match.LastResultUpdatedAt = now;
            updated++;
        }

        return (updated, matched);
    }

    /// <summary>
    /// Resolves an external row to a match in the round. The provider's id is the reliable key —
    /// it is stored when the fixture is imported — and the team names are the fallback for matches
    /// added by hand or imported before ids were kept.
    /// </summary>
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

        var home = FootballReference.Canonical(result.HomeTeamName);
        var away = FootballReference.Canonical(result.AwayTeamName);
        return round.Matches.FirstOrDefault(m =>
            FootballReference.Canonical(m.HomeTeam?.Name ?? string.Empty) == home
            && FootballReference.Canonical(m.AwayTeam?.Name ?? string.Empty) == away
            // The same two clubs can meet in the league and in the cup, and the provider is asked
            // for every competition in the round, so the names alone are not enough.
            && (result.Competition is null || m.Competition == result.Competition)
            // A match already identified in this provider's own numbering is settled: a row that
            // only agrees on the names is a different game.
            && !IsBoundToAnotherMatch(m.ExternalMatchId, result.ExternalMatchId));
    }

    /// <summary>
    /// True when the match already carries an id from the same source as the incoming row, but a
    /// different one. Ids from another source (fixtures imported from a different provider) say
    /// nothing about this feed and must not block the name fallback.
    /// </summary>
    private static bool IsBoundToAnotherMatch(string? storedId, string? incomingId)
        => storedId is not null
            && incomingId is not null
            && storedId != incomingId
            && IdSource(storedId) == IdSource(incomingId);

    private static string IdSource(string externalId)
    {
        var dash = externalId.IndexOf('-');
        return dash > 0 ? externalId[..dash] : externalId;
    }
}
