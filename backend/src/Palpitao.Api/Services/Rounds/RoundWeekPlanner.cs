using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Rounds;

/// <summary>A season's round as the regrouping planner sees it.</summary>
public sealed record RoundSlot(Guid Id, int Number, int Part, RoundStatus Status, DateTime CreatedAt);

/// <summary>One round whose number or part changes.</summary>
public sealed record RoundMove(Guid Id, int FromNumber, int FromPart, int ToNumber, int ToPart);

/// <summary>
/// A regrouping worked out but not applied: where the round lands, every round that moves
/// (the round itself included) and whether a Scored round is involved — then the season has to
/// be replayed, since absences, penalties and the round thresholds depend on the numbering.
/// </summary>
public sealed record RoundWeekPlan(
    string? Error, int TargetNumber, int TargetPart, IReadOnlyList<RoundMove> Moves, bool RequiresReplay)
{
    public bool Allowed => Error is null;

    public static RoundWeekPlan Refused(string error) => new(error, 0, 0, [], false);
}

/// <summary>
/// Works out how a round joins the previous round as its next part ("11" → "10.2") or leaves
/// its round played in parts ("10.2" → "11"), renumbering the rounds after it so the numbering
/// has no gap. Pure: <see cref="RoundWeekService"/> applies the plan and replays the season.
/// Cancelled rounds keep a slot in the unique (season, number, part) index, so they move along
/// with the rest; they just never count as a part that decides anything.
/// </summary>
public static class RoundWeekPlanner
{
    public static RoundWeekPlan PlanJoinPrevious(IReadOnlyList<RoundSlot> season, Guid roundId)
    {
        var round = season.Single(r => r.Id == roundId);
        if (round.Status == RoundStatus.Cancelled)
        {
            return RoundWeekPlan.Refused("round.regroupCancelled");
        }

        if (round.Part != 0)
        {
            return RoundWeekPlan.Refused("round.joinOnlyStandalone");
        }

        var previousNumber = season
            .Where(r => r.Number < round.Number && r.Status != RoundStatus.Cancelled)
            .Select(r => (int?)r.Number)
            .Max();
        if (previousNumber is not { } target)
        {
            return RoundWeekPlan.Refused("round.noPreviousWeek");
        }

        var destination = new Dictionary<Guid, (int Number, int Part)>();

        // The previous round's members become parts 1..k (a standalone round turns into part 1)
        // and the round joins after them.
        var members = season
            .Where(r => r.Number == target)
            .OrderBy(r => r.Part)
            .ThenBy(r => r.CreatedAt)
            .ToList();
        for (var i = 0; i < members.Count; i++)
        {
            destination[members[i].Id] = (target, i + 1);
        }

        destination[round.Id] = (target, members.Count + 1);

        // Close the gap the round leaves behind, unless something else still holds its number.
        if (!season.Any(r => r.Id != round.Id && r.Number == round.Number))
        {
            foreach (var later in season.Where(r => r.Number > round.Number))
            {
                destination[later.Id] = (later.Number - 1, later.Part);
            }
        }

        return Build(season, round, destination, involved: members);
    }

    public static RoundWeekPlan PlanLeave(IReadOnlyList<RoundSlot> season, Guid roundId)
    {
        var round = season.Single(r => r.Id == roundId);
        if (round.Status == RoundStatus.Cancelled)
        {
            return RoundWeekPlan.Refused("round.regroupCancelled");
        }

        var activeParts = season
            .Where(r => r.Number == round.Number && r.Status != RoundStatus.Cancelled)
            .ToList();
        if (round.Part == 0 || activeParts.Count < 2 || activeParts.Max(r => r.Part) != round.Part)
        {
            return RoundWeekPlan.Refused("round.leaveOnlyLastPart");
        }

        var destination = new Dictionary<Guid, (int Number, int Part)>();

        // Make room right after the round it leaves, then sit there as a standalone round.
        foreach (var later in season.Where(r => r.Number > round.Number))
        {
            destination[later.Id] = (later.Number + 1, later.Part);
        }

        destination[round.Id] = (round.Number + 1, 0);

        // What stays is renumbered 1..k, or goes back to standalone when a single round is left.
        var remaining = season
            .Where(r => r.Number == round.Number && r.Id != round.Id)
            .OrderBy(r => r.Part)
            .ThenBy(r => r.CreatedAt)
            .ToList();
        for (var i = 0; i < remaining.Count; i++)
        {
            destination[remaining[i].Id] = (round.Number, remaining.Count == 1 ? 0 : i + 1);
        }

        return Build(season, round, destination, involved: remaining);
    }

    private static RoundWeekPlan Build(
        IReadOnlyList<RoundSlot> season,
        RoundSlot round,
        Dictionary<Guid, (int Number, int Part)> destination,
        IReadOnlyList<RoundSlot> involved)
    {
        var moves = season
            .Where(r => destination.TryGetValue(r.Id, out var to) && (to.Number, to.Part) != (r.Number, r.Part))
            .Select(r => new RoundMove(r.Id, r.Number, r.Part, destination[r.Id].Number, destination[r.Id].Part))
            .OrderBy(m => m.ToNumber)
            .ThenBy(m => m.ToPart)
            .ToList();

        // The plan is only as good as the numbering it leaves behind.
        var final = season.Select(r => destination.TryGetValue(r.Id, out var to) ? to : (r.Number, r.Part)).ToList();
        if (final.Distinct().Count() != final.Count || final.Any(f => f.Number < 1))
        {
            return RoundWeekPlan.Refused("round.duplicateNumber");
        }

        // A Scored round that moves, or that shares the round's number and so sees another part
        // take or give up the decision on absences, has to be replayed.
        var movedIds = moves.Select(m => m.Id).ToHashSet();
        var requiresReplay = season
            .Where(r => movedIds.Contains(r.Id) || involved.Any(i => i.Id == r.Id))
            .Any(r => r.Status == RoundStatus.Scored);

        var target = destination[round.Id];
        return new RoundWeekPlan(null, target.Number, target.Part, moves, requiresReplay);
    }
}
