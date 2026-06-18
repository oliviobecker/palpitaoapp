using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Tournaments;

namespace Palpitao.Api.Services.Rounds;

public class RoundService : IRoundService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public RoundService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    // -----------------------------------------------------------------------
    // Queries
    // -----------------------------------------------------------------------
    public async Task<IReadOnlyList<RoundSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Rounds
            .Where(r => r.GroupId == groupId)
            .OrderBy(r => r.Number)
            .Select(r => new RoundSummaryDto
            {
                Id = r.Id,
                SeasonId = r.SeasonId,
                Number = r.Number,
                Title = r.Title,
                Status = r.Status,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                FirstMatchStartsAt = r.FirstMatchStartsAt,
                PublishedAt = r.PublishedAt,
                LockedAt = r.LockedAt,
                MatchCount = r.Matches.Count,
                TournamentType = r.Season!.TournamentType,
                AllowParticipantsToViewOthersPredictions = r.Season!.AllowParticipantsToViewOthersPredictions,
                AllowParticipantsToSubmitPredictions = r.Season!.AllowParticipantsToSubmitPredictions,
            })
            .ToListAsync(ct);
    }

    public async Task<RoundDto> GetByIdAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        var dto = MapRound(round);
        var season = await _db.Seasons
            .Where(s => s.Id == round.SeasonId)
            .Select(s => new { s.TournamentType, s.AllowParticipantsToViewOthersPredictions, s.AllowParticipantsToSubmitPredictions })
            .FirstAsync(ct);
        dto.TournamentType = season.TournamentType;
        dto.AllowParticipantsToViewOthersPredictions = season.AllowParticipantsToViewOthersPredictions;
        dto.AllowParticipantsToSubmitPredictions = season.AllowParticipantsToSubmitPredictions;
        dto.Flavio = await BuildFlavioAsync(round, ct);
        return dto;
    }

    /// <summary>
    /// Flávio-rule info for the group message: only from round 16 onwards, with the
    /// current standings leader(s) and their special deadline (once published).
    /// </summary>
    private async Task<RoundFlavioDto?> BuildFlavioAsync(Round round, CancellationToken ct)
    {
        if (round.Number < FlavioRuleService.FirstApplicableRound)
        {
            return null;
        }

        var topPoints = await _db.Standings
            .Where(s => s.SeasonId == round.SeasonId)
            .Select(s => (int?)s.TotalPoints)
            .MaxAsync(ct);

        var leaderNames = new List<string>();
        if (topPoints is not null)
        {
            // Eliminated participants (per-group flag) are not named as leaders.
            leaderNames = await _db.Standings
                .Where(s => s.SeasonId == round.SeasonId && s.TotalPoints == topPoints)
                .Where(s => _db.GroupUsers.Any(gu =>
                    gu.GroupId == round.GroupId && gu.UserId == s.UserId && !gu.IsEliminated))
                .Join(_db.Users, s => s.UserId, u => u.Id, (s, u) => u.Name)
                .ToListAsync(ct);
        }

        return new RoundFlavioDto
        {
            Applies = true,
            LeaderNames = leaderNames,
            DeadlineUtc = FlavioRuleService.TryComputeEffectiveDeadline(round),
        };
    }

    // -----------------------------------------------------------------------
    // Round lifecycle
    // -----------------------------------------------------------------------
    public async Task<RoundDto> CreateAsync(CreateRoundRequest request, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);

        var seasonExists = await _db.Seasons.AnyAsync(s => s.Id == request.SeasonId && s.GroupId == groupId, ct);
        if (!seasonExists)
        {
            throw new NotFoundException("notFound.season");
        }

        var numberTaken = await _db.Rounds
            .AnyAsync(r => r.SeasonId == request.SeasonId && r.Number == request.Number, ct);
        if (numberTaken)
        {
            throw new BusinessRuleException("round.duplicateNumber");
        }

        var (startDate, endDate) = ValidatePeriod(request.StartDate, request.EndDate);

        var round = new Round
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonId = request.SeasonId,
            Number = request.Number,
            Title = request.Title,
            StartDate = startDate,
            EndDate = endDate,
            Status = RoundStatus.Draft,
            CreatedByUserId = actingUserId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Rounds.Add(round);
        _audit.Add(actingUserId, "RoundCreated", nameof(Round), round.Id.ToString(),
            new { round.Number, round.Title, round.SeasonId });
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(round.Id, ct);
    }

    public async Task<RoundDto> UpdateAsync(Guid roundId, UpdateRoundRequest request, Guid actingUserId, CancellationToken ct)
    {
        var round = await LoadRoundWithMatches(roundId, ct);

        if (round.Status is RoundStatus.Locked or RoundStatus.Scored or RoundStatus.Cancelled)
        {
            throw new BusinessRuleException("round.cannotEditClosed");
        }

        if (request.Number != round.Number)
        {
            var numberTaken = await _db.Rounds
                .AnyAsync(r => r.SeasonId == round.SeasonId && r.Number == request.Number && r.Id != round.Id, ct);
            if (numberTaken)
            {
                throw new BusinessRuleException("round.duplicateNumber");
            }
        }

        var (startDate, endDate) = ValidatePeriod(request.StartDate, request.EndDate);

        round.Number = request.Number;
        round.Title = request.Title;
        round.StartDate = startDate;
        round.EndDate = endDate;

        _audit.Add(actingUserId, "RoundUpdated", nameof(Round), round.Id.ToString(),
            new { round.Number, round.Title, round.StartDate, round.EndDate });
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(round.Id, ct);
    }

    public async Task<RoundDto> PublishAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
    {
        var round = await LoadRoundWithMatches(roundId, ct);

        if (round.Status != RoundStatus.Draft)
        {
            throw new BusinessRuleException("round.onlyDraftPublished");
        }

        if (round.Matches.Count == 0)
        {
            throw new BusinessRuleException("round.needsMatchToPublish");
        }

        round.FirstMatchStartsAt = round.Matches.Min(m => m.StartsAt);
        round.PublishedAt = DateTime.UtcNow;
        round.Status = RoundStatus.Published;

        // Regra Flávio: capture applicability and the target leader at publication
        // so a mid-round standings change does not move the target.
        var tournamentType = await _db.Seasons
            .Where(s => s.Id == round.SeasonId)
            .Select(s => s.TournamentType)
            .FirstAsync(ct);

        round.FlavioRuleApplies = tournamentType == TournamentType.FifaWorldCup
            ? round.Matches.Any(m => TournamentRules.IsWorldCupFlavioPhase(m.Phase))
            : round.Number >= FlavioRuleService.FirstApplicableRound;

        round.FlavioRuleTargetUserId = round.FlavioRuleApplies
            ? await _db.Standings
                .Where(s => s.SeasonId == round.SeasonId)
                .OrderByDescending(s => s.TotalPoints)
                .ThenBy(s => s.UserId)
                .Select(s => (Guid?)s.UserId)
                .FirstOrDefaultAsync(ct)
            : null;

        round.FlavioDeadlineUtc = round.FlavioRuleApplies
            ? FlavioRuleService.TryComputeEffectiveDeadline(round)
            : null;

        _audit.Add(actingUserId, "RoundPublished", nameof(Round), round.Id.ToString(),
            new { round.FirstMatchStartsAt, round.FlavioRuleApplies, round.FlavioRuleTargetUserId });
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(round.Id, ct);
    }

    public async Task<RoundDto> LockAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
    {
        var round = await LoadRoundWithMatches(roundId, ct);

        if (round.Status != RoundStatus.Published)
        {
            throw new BusinessRuleException("round.onlyPublishedLocked");
        }

        round.LockedAt = DateTime.UtcNow;
        round.Status = RoundStatus.Locked;

        _audit.Add(actingUserId, "RoundLocked", nameof(Round), round.Id.ToString(),
            new { round.LockedAt });
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(round.Id, ct);
    }

    public async Task<RoundDto> CancelAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
    {
        var round = await LoadRoundWithMatches(roundId, ct);

        if (round.Status == RoundStatus.Scored)
        {
            throw new BusinessRuleException("round.cannotCancelScored");
        }

        if (round.Status == RoundStatus.Cancelled)
        {
            throw new BusinessRuleException("round.alreadyCancelled");
        }

        round.Status = RoundStatus.Cancelled;

        _audit.Add(actingUserId, "RoundCancelled", nameof(Round), round.Id.ToString(), null);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(round.Id, ct);
    }

    public async Task<RoundDto> ReopenAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
    {
        var round = await LoadRoundWithMatches(roundId, ct);

        if (round.Status != RoundStatus.Scored)
        {
            throw new BusinessRuleException("round.onlyScoredReopened");
        }

        // Reopening only steps the round back to Locked; the existing scores and
        // standings stay untouched until the admin re-scores (idempotent re-score
        // clears this round's results and recomputes the standings).
        round.Status = RoundStatus.Locked;

        _audit.Add(actingUserId, "RoundReopened", nameof(Round), round.Id.ToString(), null);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(round.Id, ct);
    }

    // -----------------------------------------------------------------------
    // Matches
    // -----------------------------------------------------------------------
    public async Task<MatchDto> AddMatchAsync(Guid roundId, CreateMatchRequest request, Guid actingUserId, CancellationToken ct)
    {
        var round = await LoadRoundWithMatches(roundId, ct);

        var usedOverride = EnsureMatchesEditable(round, request.OverrideLockJustification);

        var (competition, phase, startsAt) = ValidateMatchBasics(request);
        await ValidateCompetitionAndPhaseAsync(round.SeasonId, competition, phase, ct);
        await ValidateTeams(request.HomeTeamId, request.AwayTeamId, ct);
        ValidateManualMultiplier(request.ManualMultiplierOverride, request.ManualMultiplierJustification);
        ValidateLeagueOneLimit(round, competition, request.ManualMultiplierOverride, request.ManualMultiplierJustification, excludeMatchId: null);

        var match = new RoundMatch
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            Competition = competition,
            Phase = phase,
            HomeTeamId = request.HomeTeamId,
            AwayTeamId = request.AwayTeamId,
            StartsAt = startsAt,
            Order = request.Order,
            ManualMultiplierOverride = request.ManualMultiplierOverride,
            ManualMultiplierJustification = request.ManualMultiplierJustification,
            CreatedAt = DateTime.UtcNow,
        };

        round.Matches.Add(match);
        // Add explicitly so EF tracks it as Added (a client-set Guid key on a
        // navigation-only add would otherwise be inferred as Modified).
        _db.RoundMatches.Add(match);
        RecalculateFirstMatch(round);

        _audit.Add(actingUserId, "MatchAdded", nameof(RoundMatch), match.Id.ToString(),
            new { round = round.Id, competition, phase, request.HomeTeamId, request.AwayTeamId, startsAt, overrideLock = usedOverride });
        await _db.SaveChangesAsync(ct);

        return await GetMatchDto(match.Id, ct);
    }

    public async Task<MatchDto> UpdateMatchAsync(Guid matchId, UpdateMatchRequest request, Guid actingUserId, CancellationToken ct)
    {
        var (round, match) = await LoadMatchWithRound(matchId, ct);

        var usedOverride = EnsureMatchesEditable(round, request.OverrideLockJustification);

        var (competition, phase, startsAt) = ValidateMatchBasics(request);
        await ValidateCompetitionAndPhaseAsync(round.SeasonId, competition, phase, ct);
        await ValidateTeams(request.HomeTeamId, request.AwayTeamId, ct);
        ValidateManualMultiplier(request.ManualMultiplierOverride, request.ManualMultiplierJustification);
        ValidateLeagueOneLimit(round, competition, request.ManualMultiplierOverride, request.ManualMultiplierJustification, excludeMatchId: match.Id);

        match.Competition = competition;
        match.Phase = phase;
        match.HomeTeamId = request.HomeTeamId;
        match.AwayTeamId = request.AwayTeamId;
        match.StartsAt = startsAt;
        match.Order = request.Order;
        match.ManualMultiplierOverride = request.ManualMultiplierOverride;
        match.ManualMultiplierJustification = request.ManualMultiplierJustification;

        RecalculateFirstMatch(round);

        _audit.Add(actingUserId, "MatchUpdated", nameof(RoundMatch), match.Id.ToString(),
            new { round = round.Id, competition, phase, startsAt, overrideLock = usedOverride });
        await _db.SaveChangesAsync(ct);

        return await GetMatchDto(match.Id, ct);
    }

    public async Task DeleteMatchAsync(Guid matchId, string? overrideLockJustification, Guid actingUserId, CancellationToken ct)
    {
        var (round, match) = await LoadMatchWithRound(matchId, ct);

        var usedOverride = EnsureMatchesEditable(round, overrideLockJustification);

        round.Matches.Remove(match);
        _db.RoundMatches.Remove(match);
        RecalculateFirstMatch(round);

        _audit.Add(actingUserId, "MatchRemoved", nameof(RoundMatch), match.Id.ToString(),
            new { round = round.Id, overrideLock = usedOverride });
        await _db.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Validation helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Blocks match edits on Locked/Scored/Cancelled rounds unless an admin
    /// override justification is provided. Returns true when override was used.
    /// </summary>
    private static bool EnsureMatchesEditable(Round round, string? overrideJustification)
    {
        var isClosed = round.Status is RoundStatus.Locked or RoundStatus.Scored or RoundStatus.Cancelled;
        if (!isClosed)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(overrideJustification))
        {
            throw new BusinessRuleException("round.cannotEditMatchClosedNoJustification");
        }

        return true;
    }

    private static (Competition Competition, MatchPhase Phase, DateTime StartsAt) ValidateMatchBasics(CreateMatchRequest request)
    {
        if (request.Competition is null)
        {
            throw new BusinessRuleException("match.competitionRequired");
        }

        if (request.Phase is null)
        {
            throw new BusinessRuleException("match.phaseRequired");
        }

        if (request.StartsAt is null)
        {
            throw new BusinessRuleException("match.dateRequired");
        }

        if (request.HomeTeamId == request.AwayTeamId)
        {
            throw new BusinessRuleException("match.sameTeam");
        }

        return (request.Competition.Value, request.Phase.Value, ToUtc(request.StartsAt.Value));
    }

    /// <summary>Rejects competitions/phases that do not belong to the season's certame
    /// type (e.g. a Premier League match in a FIFA World Cup season, or vice-versa).</summary>
    private async Task ValidateCompetitionAndPhaseAsync(Guid seasonId, Competition competition, MatchPhase phase, CancellationToken ct)
    {
        var type = await _db.Seasons
            .Where(s => s.Id == seasonId)
            .Select(s => s.TournamentType)
            .FirstAsync(ct);

        if (!TournamentRules.IsCompetitionAllowed(type, competition))
        {
            throw new BusinessRuleException("tournament.competitionNotAllowed");
        }

        if (!TournamentRules.IsPhaseAllowed(type, phase))
        {
            throw new BusinessRuleException("tournament.phaseNotAllowed");
        }
    }

    private async Task ValidateTeams(Guid homeTeamId, Guid awayTeamId, CancellationToken ct)
    {
        var found = await _db.Teams
            .CountAsync(t => t.Id == homeTeamId || t.Id == awayTeamId, ct);

        // Both ids are distinct at this point, so a valid pair returns 2.
        if (found < 2)
        {
            throw new NotFoundException("notFound.team");
        }
    }

    /// <summary>
    /// Normalizes the round window to UTC and enforces EndDate &gt;= StartDate.
    /// Both are optional (legacy rounds), but if one is given the other is required.
    /// </summary>
    private static (DateTime? Start, DateTime? End) ValidatePeriod(DateTime? start, DateTime? end)
    {
        if (start is null && end is null)
        {
            return (null, null);
        }

        if (start is null || end is null)
        {
            throw new BusinessRuleException("round.startEndRequired");
        }

        var utcStart = ToUtc(start.Value);
        var utcEnd = ToUtc(end.Value);

        if (utcEnd < utcStart)
        {
            throw new BusinessRuleException("fixtures.endBeforeStart");
        }

        return (utcStart, utcEnd);
    }

    private static void ValidateManualMultiplier(int? manualOverride, string? justification)
    {
        if (manualOverride.HasValue && string.IsNullOrWhiteSpace(justification))
        {
            throw new BusinessRuleException("match.multiplierJustificationRequired");
        }
    }

    private static void ValidateLeagueOneLimit(
        Round round,
        Competition competition,
        int? manualOverride,
        string? justification,
        Guid? excludeMatchId)
    {
        if (competition != Competition.LeagueOne)
        {
            return;
        }

        var otherLeagueOneCount = round.Matches
            .Count(m => m.Competition == Competition.LeagueOne && m.Id != excludeMatchId);

        if (otherLeagueOneCount >= 1)
        {
            var hasJustifiedOverride = manualOverride.HasValue && !string.IsNullOrWhiteSpace(justification);
            if (!hasJustifiedOverride)
            {
                throw new BusinessRuleException("match.leagueOneSingle");
            }
        }
    }

    private static void RecalculateFirstMatch(Round round)
    {
        // Only meaningful once the round is published (the prediction deadline).
        if (round.Status == RoundStatus.Published)
        {
            round.FirstMatchStartsAt = round.Matches.Count > 0
                ? round.Matches.Min(m => m.StartsAt)
                : null;
        }
    }

    // -----------------------------------------------------------------------
    // Loading / mapping helpers
    // -----------------------------------------------------------------------
    private async Task<Round> LoadRoundWithMatches(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");
    }

    private async Task<(Round Round, RoundMatch Match)> LoadMatchWithRound(Guid matchId, CancellationToken ct)
    {
        var match = await _db.RoundMatches.FirstOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new NotFoundException("notFound.match");

        var round = await LoadRoundWithMatches(match.RoundId, ct);
        var tracked = round.Matches.First(m => m.Id == matchId);
        return (round, tracked);
    }

    private async Task<MatchDto> GetMatchDto(Guid matchId, CancellationToken ct)
    {
        var match = await _db.RoundMatches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstAsync(m => m.Id == matchId, ct);

        return MapMatch(match);
    }

    private static RoundDto MapRound(Round round) => new()
    {
        Id = round.Id,
        SeasonId = round.SeasonId,
        Number = round.Number,
        Title = round.Title,
        StartDate = round.StartDate,
        EndDate = round.EndDate,
        Status = round.Status,
        FirstMatchStartsAt = round.FirstMatchStartsAt,
        PublishedAt = round.PublishedAt,
        LockedAt = round.LockedAt,
        MirrorPublishedAt = round.MirrorPublishedAt,
        CreatedAt = round.CreatedAt,
        Matches = round.Matches
            .OrderBy(m => m.Order)
            .ThenBy(m => m.StartsAt)
            .Select(MapMatch)
            .ToList(),
    };

    private static MatchDto MapMatch(RoundMatch m) => new()
    {
        Id = m.Id,
        RoundId = m.RoundId,
        Competition = m.Competition,
        Phase = m.Phase,
        HomeTeamId = m.HomeTeamId,
        HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
        AwayTeamId = m.AwayTeamId,
        AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
        StartsAt = m.StartsAt,
        Order = m.Order,
        HomeScore = m.HomeScore,
        AwayScore = m.AwayScore,
        IsFinished = m.IsFinished,
        ManualMultiplierOverride = m.ManualMultiplierOverride,
        ManualMultiplierJustification = m.ManualMultiplierJustification,
    };

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
