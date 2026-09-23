using System.Data;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;

namespace Palpitao.Api.Services.Rounds;

/// <inheritdoc />
public class RoundWeekService : IRoundWeekService
{
    private readonly AppDbContext _db;
    private readonly IRoundService _rounds;
    private readonly IRoundScoringService _scoring;
    private readonly ISeasonScoringConfigService _config;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public RoundWeekService(
        AppDbContext db,
        IRoundService rounds,
        IRoundScoringService scoring,
        ISeasonScoringConfigService config,
        IAuditService audit,
        ICurrentGroupService current)
    {
        _db = db;
        _rounds = rounds;
        _scoring = scoring;
        _config = config;
        _audit = audit;
        _current = current;
    }

    public Task<RoundDto> CreateInPreviousWeekAsync(CreateRoundRequest request, Guid actingUserId, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            // Created standalone at the number the form proposed, then joined exactly like the
            // round-detail action: one path, one set of rules.
            var created = await _rounds.CreateAsync(request, actingUserId, ct);
            await RegroupAsync(created.Id, RoundWeekPlanner.PlanJoinPrevious, "RoundJoinedPreviousWeek", actingUserId, ct);
            return await _rounds.GetByIdAsync(created.Id, ct);
        }, ct);

    public Task<RoundDto> JoinPreviousWeekAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await RegroupAsync(roundId, RoundWeekPlanner.PlanJoinPrevious, "RoundJoinedPreviousWeek", actingUserId, ct);
            return await _rounds.GetByIdAsync(roundId, ct);
        }, ct);

    public Task<RoundDto> LeaveWeekAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await RegroupAsync(roundId, RoundWeekPlanner.PlanLeave, "RoundLeftWeek", actingUserId, ct);
            return await _rounds.GetByIdAsync(roundId, ct);
        }, ct);

    public Task<RoundDto> CancelAsync(Guid roundId, Guid actingUserId, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            var round = await LoadAsync(roundId, ct);

            // Taken before the cancel: a Scored part means the absences were already decided
            // with this part in the picture — by it, or by a later part that looked at it.
            var otherPartScored = await RoundWeek.Siblings(_db, round)
                .AnyAsync(r => r.Status == RoundStatus.Scored, ct);

            var dto = await _rounds.CancelAsync(roundId, actingUserId, ct);
            if (!otherPartScored)
            {
                return dto;
            }

            await _scoring.RecalculateSeasonAsync(round.SeasonId, actingUserId, ct);
            return await _rounds.GetByIdAsync(roundId, ct);
        }, ct);

    private async Task RegroupAsync(
        Guid roundId,
        Func<IReadOnlyList<RoundSlot>, Guid, RoundWeekPlan> planner,
        string action,
        Guid actingUserId,
        CancellationToken ct)
    {
        var round = await LoadAsync(roundId, ct);
        var from = RoundNames.Label(round.Number, round.Part);

        var season = await _db.Rounds
            .AsNoTracking()
            .Where(r => r.SeasonId == round.SeasonId)
            .Select(r => new RoundSlot(r.Id, r.Number, r.Part, r.Status, r.CreatedAt))
            .ToListAsync(ct);

        var plan = planner(season, round.Id);
        if (!plan.Allowed)
        {
            throw new BusinessRuleException(plan.Error!);
        }

        await ApplyAsync(round.SeasonId, plan, action, round.Id, actingUserId, ct);

        _audit.Add(actingUserId, action, nameof(Round), round.Id.ToString(), new
        {
            from,
            to = RoundNames.Label(plan.TargetNumber, plan.TargetPart),
            renumbered = plan.Moves.Count,
            recalculated = plan.RequiresReplay,
        });
        await _db.SaveChangesAsync(ct);

        // Absences, penalties and eliminations hang on which part decides and on the round
        // numbers the thresholds compare against: a Scored round in the move means replaying.
        if (plan.RequiresReplay)
        {
            await _scoring.RecalculateSeasonAsync(round.SeasonId, actingUserId, ct);
        }
    }

    /// <summary>
    /// Writes the plan in two passes. Rows are updated one at a time and the (season, number,
    /// part) index is checked row by row, so moving "12" to "11" while "11" is still there would
    /// collide mid-way; parking every moved round on a temporary negative number first leaves
    /// each final position free when it is written.
    /// </summary>
    private async Task ApplyAsync(
        Guid seasonId, RoundWeekPlan plan, string operation, Guid triggerRoundId, Guid actingUserId,
        CancellationToken ct)
    {
        var ids = plan.Moves.Select(m => m.Id).ToList();
        var rounds = await _db.Rounds.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        var parking = 0;
        foreach (var move in plan.Moves)
        {
            rounds[move.Id].Number = --parking;
        }

        await _db.SaveChangesAsync(ct);

        var tournamentType = await _db.Seasons
            .Where(s => s.Id == seasonId)
            .Select(s => s.TournamentType)
            .FirstAsync(ct);
        var rules = await _config.GetRuleParamsAsync(seasonId, ct);

        foreach (var move in plan.Moves)
        {
            var round = rounds[move.Id];
            var titleBefore = round.Title;

            round.Number = move.ToNumber;
            round.Part = move.ToPart;
            // A closed round's title cannot be edited any more, so a default "Sétima Rodada"
            // left on what is now round 6 would stay wrong for good.
            round.Title = RoundNames.RenumberDefaultTitle(round.Title, move.FromNumber, move.ToNumber);

            // Captured at publication from the number (England only; the World Cup goes by
            // phase). Kept in step, although scoring re-derives it from the number.
            if (tournamentType == TournamentType.PalpitaoEngland
                && round.PublishedAt is not null
                && move.FromNumber != move.ToNumber)
            {
                round.FlavioRuleApplies = round.Number >= rules.FlavioFromRound;
                round.FlavioDeadlineUtc = round.FlavioRuleApplies
                    ? FlavioRuleService.TryComputeEffectiveDeadline(round)
                    : null;
            }

            _audit.Add(actingUserId, "RoundRenumbered", nameof(Round), round.Id.ToString(), new
            {
                operation,
                triggerRoundId,
                before = new { Number = move.FromNumber, Part = move.FromPart, Title = titleBefore },
                after = new { round.Number, round.Part, round.Title },
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Round> LoadAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Rounds.FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");
    }

    /// <summary>
    /// Same contract as the scoring service's: the renumbering and the replay commit together or
    /// not at all, and an already open transaction (a caller composing operations) is joined.
    /// </summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await action();
        await tx.CommitAsync(ct);
        return result;
    }
}
