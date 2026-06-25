using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Tournaments;

namespace Palpitao.Api.Services.Fixtures;

public class FixtureImportService : IFixtureImportService
{
    private static readonly IReadOnlySet<Guid> NoClassicTeams = new HashSet<Guid>();

    private readonly AppDbContext _db;
    private readonly IFixtureProvider _provider;
    private readonly IScoringService _scoring;
    private readonly ISeasonScoringConfigService _config;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly FixtureOptions _options;

    public FixtureImportService(
        AppDbContext db,
        IFixtureProvider provider,
        IScoringService scoring,
        ISeasonScoringConfigService config,
        IAuditService audit,
        ICurrentGroupService current,
        IOptions<FixtureOptions> options)
    {
        _db = db;
        _provider = provider;
        _scoring = scoring;
        _config = config;
        _audit = audit;
        _current = current;
        _options = options.Value;
    }

    // -----------------------------------------------------------------------
    // Search
    // -----------------------------------------------------------------------
    public async Task<SearchFixturesResponse> SearchAsync(
        SearchFixturesRequest request, Guid actingUserId, CancellationToken ct)
    {
        if (!_options.EnableExternalFixtureImport)
        {
            throw new BusinessRuleException("fixtures.importDisabled");
        }

        if (request.StartDate is null || request.EndDate is null)
        {
            throw new BusinessRuleException("round.startEndRequired");
        }

        var start = ToUtc(request.StartDate.Value);
        var end = ToUtc(request.EndDate.Value);
        if (end < start)
        {
            throw new BusinessRuleException("fixtures.endBeforeStart");
        }

        // Only competitions the system tracks (ignore the rest).
        var competitions = request.Competitions.Count > 0
            ? request.Competitions.Distinct().ToList()
            : Enum.GetValues<Competition>().ToList();

        IReadOnlyList<FixtureCandidateDto> candidates;
        try
        {
            candidates = await _provider.SearchFixturesAsync(start, end, competitions, ct);
        }
        catch (BusinessRuleException)
        {
            _audit.Add(actingUserId, "FixtureSearchFailed", nameof(Round), request.RoundId?.ToString(),
                new { request.StartDate, request.EndDate, source = _provider.SourceName });
            await _db.SaveChangesAsync(ct);
            throw;
        }

        // Existing matches of the round being edited (to flag duplicates in the UI).
        var existingKeys = new HashSet<string>();
        if (request.RoundId is Guid roundId)
        {
            var existing = await _db.RoundMatches
                .Where(m => m.RoundId == roundId)
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .ToListAsync(ct);

            foreach (var m in existing)
            {
                existingKeys.Add(MatchKey(m.HomeTeam?.Name ?? string.Empty, m.AwayTeam?.Name ?? string.Empty, m.StartsAt));
            }
        }

        // Suggested multiplier: use the target round's season ruleset when known (so a
        // custom config is reflected); otherwise the tournament-type defaults inferred from
        // the candidate's competition. Classic detection uses the team names (the candidate
        // teams may not exist in the catalogue yet), so the values come from the table.
        var roundRuleSet = request.RoundId is Guid ruleRoundId
            ? await _config.GetRuleSetForRoundAsync(ruleRoundId, ct)
            : null;

        foreach (var c in candidates.OfType<FixtureCandidateDto>())
        {
            var homeBig = FootballReference.IsBigSeven(c.HomeTeamName);
            var awayBig = FootballReference.IsBigSeven(c.AwayTeamName);
            c.IsBigSevenMatch = homeBig && awayBig;
            var ruleSet = roundRuleSet ?? ScoringDefaults.ForTournamentType(
                c.Competition == Competition.FifaWorldCup ? TournamentType.FifaWorldCup : TournamentType.PalpitaoEngland,
                NoClassicTeams);
            c.SuggestedMultiplier = _scoring.GetMultiplier(ruleSet, c.Competition, c.Phase, homeBig, awayBig);
            c.Source = string.IsNullOrWhiteSpace(c.Source) ? _provider.SourceName : c.Source;
            c.IsAlreadyAddedToRound = existingKeys.Contains(MatchKey(c.HomeTeamName, c.AwayTeamName, c.StartsAt));
        }

        var ordered = candidates
            .OrderBy(c => c.StartsAt)
            .ThenBy(c => c.Competition)
            .ToList();

        _audit.Add(actingUserId, "FixturesSearched", nameof(Round), request.RoundId?.ToString(),
            new { request.StartDate, request.EndDate, competitions, source = _provider.SourceName, found = ordered.Count });
        await _db.SaveChangesAsync(ct);

        return new SearchFixturesResponse { Source = _provider.SourceName, Fixtures = ordered };
    }

    // -----------------------------------------------------------------------
    // Import
    // -----------------------------------------------------------------------
    public async Task<ImportFixturesResponse> ImportAsync(
        Guid roundId, ImportFixturesRequest request, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        if (round.Status is RoundStatus.Locked or RoundStatus.Scored or RoundStatus.Cancelled)
        {
            throw new BusinessRuleException("round.cannotEditClosed");
        }

        var selected = request.Fixtures ?? new List<ImportFixtureItem>();
        if (selected.Count == 0)
        {
            throw new BusinessRuleException("fixtures.selectNone");
        }

        // Reject fixtures whose competition/phase do not match the season's certame
        // type (e.g. a Premier League fixture into a FIFA World Cup round).
        var tournamentType = await _db.Seasons
            .Where(s => s.Id == round.SeasonId)
            .Select(s => s.TournamentType)
            .FirstAsync(ct);
        foreach (var item in selected)
        {
            if (!TournamentRules.IsCompetitionAllowed(tournamentType, item.Competition))
            {
                throw new BusinessRuleException("tournament.competitionNotAllowed");
            }
            if (!TournamentRules.IsPhaseAllowed(tournamentType, item.Phase))
            {
                throw new BusinessRuleException("tournament.phaseNotAllowed");
            }
        }

        // Keys already in the round + accumulating as we add (dedupe within batch).
        var seen = new HashSet<string>();
        foreach (var m in round.Matches)
        {
            seen.Add(MatchKey(m.HomeTeam?.Name ?? string.Empty, m.AwayTeam?.Name ?? string.Empty, m.StartsAt));
        }

        // League One guard: how many would the round end up with?
        var leagueOneExisting = round.Matches.Count(m => m.Competition == Competition.LeagueOne);

        var teamCache = await _db.Teams.ToListAsync(ct);
        var createdTeams = 0;
        var imported = 0;
        var skipped = new List<string>();
        var leagueOneAfter = leagueOneExisting;

        foreach (var item in selected)
        {
            var startsAt = ToUtc(item.StartsAt);
            var key = MatchKey(item.HomeTeamName, item.AwayTeamName, startsAt);
            if (seen.Contains(key))
            {
                skipped.Add($"{item.HomeTeamName} x {item.AwayTeamName}");
                continue;
            }

            if (string.Equals(FootballReference.Normalize(item.HomeTeamName), FootballReference.Normalize(item.AwayTeamName), StringComparison.Ordinal))
            {
                throw new BusinessRuleException("match.sameTeam");
            }

            if (item.Competition == Competition.LeagueOne)
            {
                leagueOneAfter++;
                if (leagueOneAfter > 1 && string.IsNullOrWhiteSpace(request.LeagueOneJustification))
                {
                    throw new BusinessRuleException("fixtures.leagueOneSingle");
                }
            }

            var home = ResolveTeam(teamCache, item.HomeTeamName, item.Competition, ref createdTeams, actingUserId);
            var away = ResolveTeam(teamCache, item.AwayTeamName, item.Competition, ref createdTeams, actingUserId);

            var match = new RoundMatch
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                Competition = item.Competition,
                Phase = item.Phase,
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                StartsAt = startsAt,
                Order = round.Matches.Count + imported,
                CreatedAt = DateTime.UtcNow,
            };
            // Set navigation so the dedupe key reflects newly added rows too.
            match.HomeTeam = home;
            match.AwayTeam = away;
            round.Matches.Add(match);
            _db.RoundMatches.Add(match);

            seen.Add(key);
            imported++;
        }

        if (imported == 0)
        {
            // Everything selected was a duplicate.
            _audit.Add(actingUserId, "FixturesImportSkipped", nameof(Round), round.Id.ToString(),
                new { skippedDuplicates = skipped });
            await _db.SaveChangesAsync(ct);
            return new ImportFixturesResponse
            {
                ImportedCount = 0,
                SkippedDuplicateCount = skipped.Count,
                CreatedTeamCount = 0,
                SkippedDuplicates = skipped,
            };
        }

        // Recompute the prediction deadline when the round is already published.
        if (round.Status == RoundStatus.Published && round.Matches.Count > 0)
        {
            round.FirstMatchStartsAt = round.Matches.Min(m => m.StartsAt);
        }

        _audit.Add(actingUserId, "FixturesImported", nameof(Round), round.Id.ToString(),
            new { imported, skippedDuplicates = skipped, createdTeams, source = _provider.SourceName });
        await _db.SaveChangesAsync(ct);

        return new ImportFixturesResponse
        {
            ImportedCount = imported,
            SkippedDuplicateCount = skipped.Count,
            CreatedTeamCount = createdTeams,
            SkippedDuplicates = skipped,
        };
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private Team ResolveTeam(List<Team> cache, string name, Competition competition, ref int createdCount, Guid actingUserId)
    {
        var normalized = FootballReference.Normalize(name);
        var existing = cache.FirstOrDefault(t => FootballReference.Normalize(t.Name) == normalized);
        if (existing is not null)
        {
            return existing;
        }

        var trimmed = name.Trim();
        var isNationalTeam = competition == Competition.FifaWorldCup;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            ShortName = BuildShortName(trimmed),
            IsBigSevenClub = !isNationalTeam && FootballReference.IsBigSeven(trimmed),
            // Cup competitions draw from every division, so we can't infer the
            // club's league from an FA Cup fixture — leave it unset in that case.
            Division = competition is Competition.PremierLeague or Competition.Championship or Competition.LeagueOne
                ? competition
                : null,
            // World Cup fixtures create national teams (titles unknown for newly
            // discovered nations; the seven world champions are already seeded).
            TeamType = isNationalTeam ? TeamType.NationalTeam : TeamType.Club,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Teams.Add(team);
        cache.Add(team);
        createdCount++;
        _audit.Add(actingUserId, "TeamAutoCreated", nameof(Team), team.Id.ToString(),
            new { team.Name, team.IsBigSevenClub });
        return team;
    }

    private static string BuildShortName(string name)
    {
        var letters = new string(name.Where(char.IsLetter).ToArray());
        var code = (letters.Length >= 3 ? letters[..3] : letters).ToUpperInvariant();
        return string.IsNullOrEmpty(code) ? "TBD" : code;
    }

    private static string MatchKey(string home, string away, DateTime startsAt)
        => $"{FootballReference.Normalize(home)}|{FootballReference.Normalize(away)}|{startsAt:yyyyMMddHHmm}";

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
