using Palpitao.Api.Common;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Rounds;
using Xunit;

namespace Palpitao.Api.Tests.Rounds;

public class RoundWeekPlannerTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RoundSlot Slot(int number, int part = 0, RoundStatus status = RoundStatus.Scored)
        => new(Guid.NewGuid(), number, part, status, Created);

    private static List<RoundSlot> Apply(IReadOnlyList<RoundSlot> season, RoundWeekPlan plan)
    {
        var moves = plan.Moves.ToDictionary(m => m.Id);
        return season
            .Select(r => moves.TryGetValue(r.Id, out var m) ? r with { Number = m.ToNumber, Part = m.ToPart } : r)
            .ToList();
    }

    /// <summary>Every round's label once the plan is applied, in season order.</summary>
    private static string[] After(IReadOnlyList<RoundSlot> season, RoundWeekPlan plan) => Apply(season, plan)
        .OrderBy(r => r.Number)
        .ThenBy(r => r.Part)
        .Select(r => RoundNames.Label(r.Number, r.Part))
        .ToArray();

    [Fact]
    public void Joining_turns_the_previous_round_into_part_one_and_closes_the_gap()
    {
        var season = new[] { Slot(1), Slot(2), Slot(3) };

        var plan = RoundWeekPlanner.PlanJoinPrevious(season, season[1].Id);

        Assert.True(plan.Allowed);
        Assert.Equal((1, 2), (plan.TargetNumber, plan.TargetPart));
        Assert.Equal(["1.1", "1.2", "2"], After(season, plan));
        Assert.Equal(3, plan.Moves.Count);
        Assert.True(plan.RequiresReplay);
    }

    [Fact]
    public void Joining_a_round_already_in_parts_appends_the_next_part()
    {
        var season = new[] { Slot(1, 1), Slot(1, 2), Slot(2, status: RoundStatus.Draft) };

        var plan = RoundWeekPlanner.PlanJoinPrevious(season, season[2].Id);

        Assert.Equal(["1.1", "1.2", "1.3"], After(season, plan));
        // Only the new part moves, but the Scored parts see the decision on absences pass to it.
        Assert.Single(plan.Moves);
        Assert.True(plan.RequiresReplay);
    }

    [Fact]
    public void Nothing_scored_means_nothing_to_replay()
    {
        var season = new[] { Slot(1, status: RoundStatus.Published), Slot(2, status: RoundStatus.Draft) };

        var plan = RoundWeekPlanner.PlanJoinPrevious(season, season[1].Id);

        Assert.False(plan.RequiresReplay);
    }

    [Fact]
    public void A_cancelled_round_in_between_is_skipped_but_keeps_its_slot()
    {
        var season = new[] { Slot(1), Slot(2, status: RoundStatus.Cancelled), Slot(3), Slot(4) };

        var plan = RoundWeekPlanner.PlanJoinPrevious(season, season[2].Id);

        Assert.Equal((1, 2), (plan.TargetNumber, plan.TargetPart));
        Assert.Equal(["1.1", "1.2", "2", "3"], After(season, plan));
        Assert.DoesNotContain(plan.Moves, m => m.Id == season[1].Id);
    }

    [Fact]
    public void Leaving_makes_room_after_the_round_and_restores_a_lone_part()
    {
        var season = new[] { Slot(1, 1), Slot(1, 2), Slot(2) };

        var plan = RoundWeekPlanner.PlanLeave(season, season[1].Id);

        Assert.True(plan.Allowed);
        Assert.Equal((2, 0), (plan.TargetNumber, plan.TargetPart));
        Assert.Equal(["1", "2", "3"], After(season, plan));
    }

    [Fact]
    public void Leaving_a_round_of_three_parts_keeps_the_other_two_as_parts()
    {
        var season = new[] { Slot(1, 1), Slot(1, 2), Slot(1, 3) };

        var plan = RoundWeekPlanner.PlanLeave(season, season[2].Id);

        Assert.Equal(["1.1", "1.2", "2"], After(season, plan));
    }

    [Fact]
    public void A_cancelled_later_part_does_not_stop_the_last_live_part_from_leaving()
    {
        var season = new[] { Slot(1, 1), Slot(1, 2), Slot(1, 3, RoundStatus.Cancelled) };

        var plan = RoundWeekPlanner.PlanLeave(season, season[1].Id);

        Assert.True(plan.Allowed);
        // The cancelled part stays behind and is renumbered after the first one.
        Assert.Equal(["1.1", "1.2", "2"], After(season, plan));
    }

    [Fact]
    public void Join_then_leave_is_the_identity()
    {
        var season = new[] { Slot(1), Slot(2), Slot(3), Slot(4) };

        var joined = Apply(season, RoundWeekPlanner.PlanJoinPrevious(season, season[2].Id));
        var left = Apply(joined, RoundWeekPlanner.PlanLeave(joined, season[2].Id));

        Assert.Equal(season, left);
    }

    [Theory]
    [InlineData("join-part", "round.joinOnlyStandalone")]
    [InlineData("join-first", "round.noPreviousWeek")]
    [InlineData("join-cancelled", "round.regroupCancelled")]
    [InlineData("leave-standalone", "round.leaveOnlyLastPart")]
    [InlineData("leave-first-part", "round.leaveOnlyLastPart")]
    public void Refusals(string scenario, string error)
    {
        var season = new[] { Slot(1), Slot(2, 1), Slot(2, 2), Slot(3, status: RoundStatus.Cancelled) };

        var plan = scenario switch
        {
            "join-part" => RoundWeekPlanner.PlanJoinPrevious(season, season[2].Id),
            "join-first" => RoundWeekPlanner.PlanJoinPrevious(season, season[0].Id),
            "join-cancelled" => RoundWeekPlanner.PlanJoinPrevious(season, season[3].Id),
            "leave-standalone" => RoundWeekPlanner.PlanLeave(season, season[0].Id),
            "leave-first-part" => RoundWeekPlanner.PlanLeave(season, season[1].Id),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        Assert.False(plan.Allowed);
        Assert.Equal(error, plan.Error);
        Assert.Empty(plan.Moves);
    }
}
