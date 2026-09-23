using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

/// <summary>
/// A round played in parts ("10.1" + "10.2", the two lists of a week) counts as one round for
/// absences: absent only by missing every part, recorded once by the last part.
/// </summary>
public partial class RoundScoringServiceTests
{
    private static readonly DateTime Published = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RoundWeekService Weeks(AppDbContext db, Kit kit)
    {
        var current = new FakeCurrentGroupService();
        return new RoundWeekService(
            db, kit.Rounds, kit.Scoring, TestServices.ScoringConfig(db, current), new AuditService(db), current);
    }

    private static void Excuse(AppDbContext db, Round round, Guid user, bool isAbsent)
    {
        db.AbsenceOverrides.Add(new AbsenceOverride
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            UserId = user,
            IsAbsent = isAbsent,
            Justification = "Decisão do admin",
            CreatedByUserId = Admin,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private static RoundParticipantResult ResultOf(AppDbContext db, Round round, Guid user)
        => db.RoundParticipantResults.Single(r => r.RoundId == round.Id && r.UserId == user);

    [Fact]
    public async Task Missing_every_part_is_one_absence_recorded_by_the_last_part()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var present = CreateParticipant(db, "Presente");
        var absent = CreateParticipant(db, "Ausente");
        var first = InsertLockedRound(db, 10, Published, 1, 0, part: 1);
        var second = InsertLockedRound(db, 10, Published.AddDays(3), 2, 1, part: 2);
        InsertPrediction(db, first, present, 1, 0, Published.AddHours(1));
        InsertPrediction(db, second, present, 2, 1, Published.AddDays(3).AddHours(1));

        await kit.Scoring.ScoreRoundAsync(first.Id, Admin, Ct);

        // 10.2 is still to come, so missing 10.1 alone is no absence: just a zeroed part.
        Assert.False(ResultOf(db, first, absent).WasAbsent);
        Assert.Equal(0, ResultOf(db, first, absent).FinalPoints);
        Assert.Empty(db.Absences);

        await kit.Scoring.ScoreRoundAsync(second.Id, Admin, Ct);

        var absence = Assert.Single(db.Absences);
        Assert.Equal((second.Id, absent, 1), (absence.RoundId, absence.UserId, absence.AbsenceNumber));
        Assert.True(ResultOf(db, second, absent).WasAbsent);
        Assert.False(ResultOf(db, first, absent).WasAbsent);

        var standings = await kit.Standings.GetStandingsAsync(SeasonId, Ct);
        Assert.Equal((0, 1), standings.Where(s => s.UserId == absent).Select(s => (s.PlayedRounds, s.AbsenceCount)).Single());
        // Both parts played are still one round played.
        Assert.Equal((1, 0), standings.Where(s => s.UserId == present).Select(s => (s.PlayedRounds, s.AbsenceCount)).Single());
    }

    [Fact]
    public async Task Sending_one_part_is_enough_to_be_present_in_the_round()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var onlyFirst = CreateParticipant(db, "Só a primeira");
        var onlySecond = CreateParticipant(db, "Só a segunda");
        var first = InsertLockedRound(db, 10, Published, 1, 0, part: 1);
        var second = InsertLockedRound(db, 10, Published.AddDays(3), 2, 1, part: 2);
        InsertPrediction(db, first, onlyFirst, 1, 0, Published.AddHours(1));
        InsertPrediction(db, second, onlySecond, 2, 1, Published.AddDays(3).AddHours(1));

        await kit.Scoring.ScoreRoundAsync(first.Id, Admin, Ct);
        await kit.Scoring.ScoreRoundAsync(second.Id, Admin, Ct);

        Assert.Empty(db.Absences);
        // The part they missed scores 0 — like an incomplete set — and is not an absence.
        Assert.Equal((0, false), (ResultOf(db, second, onlyFirst).FinalPoints, ResultOf(db, second, onlyFirst).WasAbsent));
        Assert.Equal((0, false), (ResultOf(db, first, onlySecond).FinalPoints, ResultOf(db, first, onlySecond).WasAbsent));
        Assert.Equal(3, ResultOf(db, first, onlyFirst).FinalPoints);
        Assert.Equal(3, ResultOf(db, second, onlySecond).FinalPoints);

        var standings = await kit.Standings.GetStandingsAsync(SeasonId, Ct);
        Assert.All(standings, s => Assert.Equal((1, 0), (s.PlayedRounds, s.AbsenceCount)));
    }

    [Fact]
    public async Task Excusing_one_part_excuses_the_whole_round()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var first = InsertLockedRound(db, 10, Published, 1, 0, part: 1);
        var second = InsertLockedRound(db, 10, Published.AddDays(3), 2, 1, part: 2);
        Excuse(db, first, user, isAbsent: false);

        await kit.Scoring.ScoreRoundAsync(first.Id, Admin, Ct);
        await kit.Scoring.ScoreRoundAsync(second.Id, Admin, Ct);

        Assert.Empty(db.Absences);
        Assert.False(ResultOf(db, second, user).WasAbsent);
    }

    [Fact]
    public async Task A_part_forced_absent_is_zeroed_but_the_round_is_not_an_absence_when_another_part_was_sent()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var first = InsertLockedRound(db, 10, Published, 1, 0, part: 1);
        var second = InsertLockedRound(db, 10, Published.AddDays(3), 2, 1, part: 2);
        InsertPrediction(db, first, user, 1, 0, Published.AddHours(1));
        InsertPrediction(db, second, user, 2, 1, Published.AddDays(3).AddHours(1));
        Excuse(db, first, user, isAbsent: true);

        await kit.Scoring.ScoreRoundAsync(first.Id, Admin, Ct);
        await kit.Scoring.ScoreRoundAsync(second.Id, Admin, Ct);

        Assert.Equal(0, ResultOf(db, first, user).FinalPoints);
        Assert.Equal(3, ResultOf(db, second, user).FinalPoints);
        Assert.Empty(db.Absences);
    }

    [Fact]
    public async Task The_last_part_cannot_be_finalized_while_another_part_still_takes_predictions()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var weeks = Weeks(db, kit);
        CreateParticipant(db);
        var ten = await PublishedRound(kit, 10);
        var eleven = await PublishedRound(kit, 11);
        await weeks.JoinPreviousWeekAsync(eleven.Id, Admin, Ct);
        await kit.Rounds.LockAsync(eleven.Id, Admin, Ct);
        await SetResults(kit, eleven, (1, 0));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => kit.Scoring.ScoreRoundAsync(eleven.Id, Admin, Ct));
        Assert.Equal("round.weekPartsOpen", ex.Key);

        // Locked is enough: predictions are closed there, so the verdict can no longer change.
        await kit.Rounds.LockAsync(ten.Id, Admin, Ct);
        var results = await kit.Scoring.ScoreRoundAsync(eleven.Id, Admin, Ct);
        Assert.Equal(RoundStatus.Scored, results.Status);
    }

    [Fact]
    public async Task Finalizing_an_earlier_part_after_the_last_one_replays_the_decision()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var first = InsertLockedRound(db, 10, Published, 1, 0, part: 1);
        var second = InsertLockedRound(db, 10, Published.AddDays(3), 2, 1, part: 2);

        // The last part finalized first: nothing anywhere yet, so they are absent.
        await kit.Scoring.ScoreRoundAsync(second.Id, Admin, Ct);
        Assert.Single(db.Absences);

        // Then the admin enters what they sent for 10.1 on WhatsApp, while it was still Locked.
        InsertPrediction(db, first, user, 1, 0, Published.AddHours(1));
        await kit.Scoring.ScoreRoundAsync(first.Id, Admin, Ct);

        db.ChangeTracker.Clear();
        Assert.Empty(db.Absences);
        Assert.False(ResultOf(db, second, user).WasAbsent);
        Assert.Contains(db.AuditLogs, a => a.Action == "SeasonRecalculated");
    }

    [Fact]
    public async Task A_round_in_parts_climbs_the_ladder_once_and_the_replay_keeps_it_that_way()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var user = CreateParticipant(db);
        var r1 = InsertLockedRound(db, 1, Published, 1, 0);
        var r21 = InsertLockedRound(db, 2, Published.AddDays(7), 1, 0, part: 1);
        var r22 = InsertLockedRound(db, 2, Published.AddDays(10), 1, 0, part: 2);
        var r3 = InsertLockedRound(db, 3, Published.AddDays(14), 1, 0);

        foreach (var round in new[] { r1, r21, r22, r3 })
        {
            await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);
        }

        // Four lists missed, three rounds: the penalty band starts at the third — one −20, not two.
        await AssertLadder();

        await kit.Scoring.RecalculateSeasonAsync(SeasonId, Admin, Ct);
        db.ChangeTracker.Clear();
        await AssertLadder();

        async Task AssertLadder()
        {
            var row = (await kit.Standings.GetStandingsAsync(SeasonId, Ct)).Single(s => s.UserId == user);
            Assert.Equal((3, 20), (row.AbsenceCount, row.PenaltyPoints));
            Assert.Equal(
                new[] { (r1.Id, 1), (r22.Id, 2), (r3.Id, 3) },
                db.Absences.OrderBy(a => a.AbsenceNumber).Select(a => new { a.RoundId, a.AbsenceNumber })
                    .AsEnumerable().Select(a => (a.RoundId, a.AbsenceNumber)));
        }
    }

    [Fact]
    public async Task The_flavio_target_of_every_part_is_the_leader_before_the_round()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var ana = CreateParticipant(db, "Ana");
        var bruno = CreateParticipant(db, "Bruno");

        var r15 = InsertLockedRound(db, 15, Published, 2, 1);
        InsertPrediction(db, r15, ana, 2, 1, Published.AddHours(1)); // exact -> 3
        InsertPrediction(db, r15, bruno, 0, 1, Published.AddHours(1)); // wrong -> 0
        await kit.Scoring.ScoreRoundAsync(r15.Id, Admin, Ct);

        var r161 = InsertLockedRound(db, 16, Published.AddDays(7), 0, 0, part: 1);
        InsertPrediction(db, r161, ana, 1, 0, Published.AddDays(7).AddHours(1)); // wrong -> 0
        InsertPrediction(db, r161, bruno, 0, 0, Published.AddDays(7).AddHours(1)); // exact 0x0 -> 5
        await kit.Scoring.ScoreRoundAsync(r161.Id, Admin, Ct);

        // Bruno leads after 16.1, but 16.2 is still round 16: its target is the leader before it.
        var r162 = InsertLockedRound(db, 16, Published.AddDays(10), 2, 1, part: 2);
        Assert.Equal([ana], await FlavioLeaders.GetBeforeRoundAsync(db, r162.Id, Ct));
        Assert.Equal([ana], await FlavioLeaders.GetBeforeRoundAsync(db, r161.Id, Ct));
    }

    [Fact]
    public async Task Grouping_scored_rounds_renumbers_the_rest_and_merges_their_absences()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var weeks = Weeks(db, kit);
        var user = CreateParticipant(db);
        var rounds = new[]
        {
            InsertLockedRound(db, 1, Published, 1, 0),
            InsertLockedRound(db, 2, Published.AddDays(3), 1, 0),
            InsertLockedRound(db, 3, Published.AddDays(7), 1, 0),
            InsertLockedRound(db, 4, Published.AddDays(14), 1, 0),
        };
        rounds[0].Title = "Primeira Rodada";
        rounds[1].Title = "Segunda Rodada";
        rounds[2].Title = "Clássico de sábado";
        rounds[3].Title = "Quarta Rodada";
        db.SaveChanges();
        foreach (var round in rounds)
        {
            await kit.Scoring.ScoreRoundAsync(round.Id, Admin, Ct);
        }

        Assert.Equal((4, 40), await AbsencesAndPenalty());

        var joined = await weeks.JoinPreviousWeekAsync(rounds[1].Id, Admin, Ct);

        Assert.Equal((1, 2), (joined.Number, joined.Part));
        db.ChangeTracker.Clear();
        Assert.Equal(
            new[]
            {
                (1, 1, "Primeira Rodada"),
                (1, 2, "Primeira Rodada"), // an untouched default title follows its number
                (2, 0, "Clássico de sábado"), // one the admin typed stays
                (3, 0, "Terceira Rodada"),
            },
            Positions(rounds));
        // Rounds 1.1 + 1.2 are one round now: three absences, a single −20.
        Assert.Equal((3, 20), await AbsencesAndPenalty());
        Assert.Equal(4, db.AuditLogs.Count(a => a.Action == "RoundRenumbered"));
        Assert.Single(db.AuditLogs, a => a.Action == "RoundJoinedPreviousWeek");

        // Leaving puts every round, title and absence back where it was.
        var left = await weeks.LeaveWeekAsync(rounds[1].Id, Admin, Ct);

        Assert.Equal((2, 0), (left.Number, left.Part));
        db.ChangeTracker.Clear();
        Assert.Equal(
            new[]
            {
                (1, 0, "Primeira Rodada"),
                (2, 0, "Segunda Rodada"),
                (3, 0, "Clássico de sábado"),
                (4, 0, "Quarta Rodada"),
            },
            Positions(rounds));
        Assert.Equal((4, 40), await AbsencesAndPenalty());

        async Task<(int, int)> AbsencesAndPenalty()
        {
            var row = (await kit.Standings.GetStandingsAsync(SeasonId, Ct)).Single(s => s.UserId == user);
            return (row.AbsenceCount, row.PenaltyPoints);
        }

        IEnumerable<(int, int, string?)> Positions(Round[] tracked) => tracked
            .Select(t => db.Rounds.Single(r => r.Id == t.Id))
            .Select(r => (r.Number, r.Part, r.Title));
    }

    [Fact]
    public async Task A_failed_replay_rolls_the_regrouping_back()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var weeks = Weeks(db, kit);
        CreateParticipant(db);
        var r1 = InsertLockedRound(db, 1, Published, 1, 0);
        var r2 = InsertLockedRound(db, 2, Published.AddDays(7), 1, 0);
        await kit.Scoring.ScoreRoundAsync(r1.Id, Admin, Ct);
        await kit.Scoring.ScoreRoundAsync(r2.Id, Admin, Ct);

        // A reopened round still holds its results, which the replay refuses to erase.
        await kit.Rounds.ReopenAsync(r1.Id, Admin, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => weeks.JoinPreviousWeekAsync(r2.Id, Admin, Ct));
        Assert.Equal("scoring.reopenedRoundPending", ex.Key);

        db.ChangeTracker.Clear();
        Assert.Equal((1, 0), db.Rounds.Where(r => r.Id == r1.Id).Select(r => new { r.Number, r.Part }).AsEnumerable().Select(r => (r.Number, r.Part)).Single());
        Assert.Equal((2, 0), db.Rounds.Where(r => r.Id == r2.Id).Select(r => new { r.Number, r.Part }).AsEnumerable().Select(r => (r.Number, r.Part)).Single());
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "RoundRenumbered");
    }

    [Fact]
    public async Task A_round_can_be_created_straight_into_the_previous_round()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var weeks = Weeks(db, kit);
        var first = await PublishedRound(kit, 1);

        var created = await weeks.CreateInPreviousWeekAsync(
            new CreateRoundRequest { SeasonId = SeasonId, Number = 2, JoinPreviousWeek = true }, Admin, Ct);

        Assert.Equal((1, 2), (created.Number, created.Part));
        Assert.True(created.Week.DecidesAbsences);
        Assert.True(created.Week.Leave.Allowed);
        Assert.Equal(2, created.Week.Leave.TargetNumber);
        Assert.False(created.Week.JoinPrevious.Allowed);

        var reloaded = await kit.Rounds.GetByIdAsync(first.Id, Ct);
        Assert.Equal((1, 1), (reloaded.Number, reloaded.Part));
        Assert.False(reloaded.Week.DecidesAbsences);
        Assert.Equal(new[] { 1, 2 }, reloaded.Week.Parts.Select(p => p.Part));

        // A part's number follows its round: it only moves by ungrouping.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => kit.Rounds.UpdateAsync(
            created.Id, new UpdateRoundRequest { Number = 5 }, Admin, Ct));
        Assert.Equal("round.partNumberLocked", ex.Key);
    }

    [Fact]
    public async Task Cancelling_the_last_part_hands_the_decision_back_to_the_previous_one()
    {
        using var db = CreateContext();
        var kit = Build(db);
        var weeks = Weeks(db, kit);
        var user = CreateParticipant(db);
        var first = InsertLockedRound(db, 10, Published, 1, 0, part: 1);
        var second = InsertLockedRound(db, 10, Published.AddDays(3), 2, 1, part: 2);
        await kit.Scoring.ScoreRoundAsync(first.Id, Admin, Ct);
        Assert.Empty(db.Absences);

        await weeks.CancelAsync(second.Id, Admin, Ct);

        db.ChangeTracker.Clear();
        var absence = Assert.Single(db.Absences);
        Assert.Equal((first.Id, user), (absence.RoundId, absence.UserId));
        Assert.True(ResultOf(db, first, user).WasAbsent);
    }
}
