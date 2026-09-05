using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Absences;

public class AbsenceServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    private static readonly (Guid Home, Guid Away)[] Pairs =
    {
        (SeedIds.Arsenal, SeedIds.Chelsea),
        (SeedIds.Liverpool, SeedIds.Newcastle),
    };

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        db.Seasons.Add(new Season
        {
            Id = SeasonId,
            Name = "England 2025/2026",
            StartDate = new DateOnly(2025, 8, 1),
            EndDate = new DateOnly(2026, 5, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return db;
    }

    private static AbsenceService Service(AppDbContext db) => new(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));

    private static Guid CreateParticipant(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = $"P{id.ToString()[..4]}",
            Email = $"user-{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        TestSeed.AddDefaultGroupMembership(db, id);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> PublishedRound(AppDbContext db, int number, int matchCount = 1)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = number }, SeedIds.AdminUser, Ct);
        for (var i = 0; i < matchCount; i++)
        {
            await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
            {
                Competition = Competition.PremierLeague,
                Phase = MatchPhase.Regular,
                HomeTeamId = Pairs[i].Home,
                AwayTeamId = Pairs[i].Away,
                StartsAt = Future.AddHours(i),
            }, SeedIds.AdminUser, Ct);
        }
        return await rounds.PublishAsync(round.Id, SeedIds.AdminUser, Ct);
    }

    /// <summary>A round closed for predictions but not yet scored -- the case this feature exists for.</summary>
    private static async Task<RoundDto> LockedRound(AppDbContext db, int number, int matchCount = 1)
    {
        var round = await PublishedRound(db, number, matchCount);
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));
        return await rounds.LockAsync(round.Id, SeedIds.AdminUser, Ct);
    }

    private static void SetStatus(AppDbContext db, Guid roundId, RoundStatus status)
    {
        db.Rounds.Single(r => r.Id == roundId).Status = status;
        db.SaveChanges();
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Absence_penalties_progress_and_eliminate_on_fifth()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var expected = new (int Number, int Penalty, bool Eliminated)[]
        {
            (1, 0, false),
            (2, 0, false),
            (3, 20, false),
            (4, 20, false),
            (5, 0, true),
        };

        foreach (var (number, penalty, eliminated) in expected)
        {
            var round = await PublishedRound(db, number);
            var outcomes = await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);

            var outcome = Assert.Single(outcomes, o => o.UserId == user);
            Assert.Equal(number, outcome.AbsenceNumber);
            Assert.Equal(penalty, outcome.PenaltyPoints);
            Assert.Equal(eliminated, outcome.Eliminated);
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));
    }

    /// <summary>Persists a custom rule set for the season (mirrors what the admin screen saves).</summary>
    private static void ConfigureRules(
        AppDbContext db, int penaltyPoints = 20, int eliminationCount = 5, int absenceFromRound = 1)
    {
        db.SeasonScoringConfigs.Add(new SeasonScoringConfig
        {
            Id = Guid.NewGuid(),
            GroupId = SeedIds.DefaultGroup,
            SeasonId = SeasonId,
            ColumnOnlyPoints = 1,
            TraditionalPoints = 3,
            MediumPoints = 5,
            UncommonPoints = 7,
            ExtraUncommonPoints = 10,
            AbsencePenaltyPoints = penaltyPoints,
            AbsenceEliminationCount = eliminationCount,
            AbsenceFromRound = absenceFromRound,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Custom_penalty_and_elimination_ordinal_are_honoured()
    {
        using var db = CreateContext();
        ConfigureRules(db, penaltyPoints: 10, eliminationCount: 3);
        var service = Service(db);
        var user = CreateParticipant(db);

        var expected = new (int Number, int Penalty, bool Eliminated)[]
        {
            (1, 0, false),
            (2, 0, false),
            (3, 0, true), // elimination is checked first, so the 3rd both eliminates and costs nothing
        };

        foreach (var (number, penalty, eliminated) in expected)
        {
            var round = await PublishedRound(db, number);
            var outcomes = await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);

            var outcome = Assert.Single(outcomes, o => o.UserId == user);
            Assert.Equal(number, outcome.AbsenceNumber);
            Assert.Equal(penalty, outcome.PenaltyPoints);
            Assert.Equal(eliminated, outcome.Eliminated);
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));
    }

    [Fact]
    public async Task Custom_penalty_applies_before_the_elimination_ordinal()
    {
        using var db = CreateContext();
        ConfigureRules(db, penaltyPoints: 10, eliminationCount: 5);
        var service = Service(db);
        var user = CreateParticipant(db);

        AbsenceOutcome? third = null;
        foreach (var number in new[] { 1, 2, 3 })
        {
            var round = await PublishedRound(db, number);
            var outcomes = await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
            third = Assert.Single(outcomes, o => o.UserId == user);
        }

        Assert.Equal(10, third!.PenaltyPoints);
        Assert.False(third.Eliminated);
    }

    [Fact]
    public async Task Rounds_before_the_threshold_zero_the_round_without_counting()
    {
        using var db = CreateContext();
        ConfigureRules(db, absenceFromRound: 3);
        var service = Service(db);
        var user = CreateParticipant(db);

        var round1 = await PublishedRound(db, 1);
        var outcome1 = Assert.Single(
            await service.ProcessRoundAbsencesAsync(round1.Id, SeedIds.AdminUser, Ct), o => o.UserId == user);

        // Not on the ladder: no ordinal, no penalty, no Absence row...
        Assert.Equal(0, outcome1.AbsenceNumber);
        Assert.Equal(0, outcome1.PenaltyPoints);
        Assert.False(outcome1.Eliminated);
        Assert.Empty(db.Absences.Where(a => a.RoundId == round1.Id));

        // ...but the participant still has a zeroed result row for the round.
        var result = db.RoundParticipantResults.Single(r => r.RoundId == round1.Id && r.UserId == user);
        Assert.True(result.WasAbsent);
        Assert.Equal(0, result.FinalPoints);

        // The first counted absence is the one in round 3, numbered 1.
        var round3 = await PublishedRound(db, 3);
        var outcome3 = Assert.Single(
            await service.ProcessRoundAbsencesAsync(round3.Id, SeedIds.AdminUser, Ct), o => o.UserId == user);
        Assert.Equal(1, outcome3.AbsenceNumber);
    }

    [Fact]
    public async Task Raising_the_threshold_and_reprocessing_clears_stale_absences()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var round = await PublishedRound(db, 1);
        await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        Assert.Single(db.Absences.Where(a => a.RoundId == round.Id));

        // The admin now starts punishing only from round 5 on, then re-scores.
        ConfigureRules(db, absenceFromRound: 5);
        await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);

        Assert.Empty(db.Absences.Where(a => a.RoundId == round.Id));
        Assert.True(db.RoundParticipantResults.Single(r => r.RoundId == round.Id && r.UserId == user).WasAbsent);
    }

    [Fact]
    public async Task Participant_with_incomplete_predictions_is_absent()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(db, 1, matchCount: 2);

        // Insert a single prediction directly (1 of 2 matches) => incomplete.
        var firstMatchId = round.Matches[0].Id;
        db.Predictions.Add(new Prediction
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            RoundMatchId = firstMatchId,
            UserId = user,
            PredictedHomeScore = 1,
            PredictedAwayScore = 0,
            SubmittedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        Assert.True(await service.IsAbsentAsync(round.Id, user, Ct));
    }

    [Fact]
    public async Task Participant_with_all_predictions_is_not_absent()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(db, 1, matchCount: 2);

        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());
        await predictions.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest
        {
            Predictions = round.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = 1,
                PredictedAwayScore = 1,
            }).ToList(),
        }, isEdit: false, Ct);

        Assert.False(await service.IsAbsentAsync(round.Id, user, Ct));
    }

    [Fact]
    public async Task Eliminated_participant_cannot_predict_future_round()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 5; i++)
        {
            var round = await PublishedRound(db, i);
            await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        }

        var future = await PublishedRound(db, 6);
        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            predictions.SavePredictionsAsync(future.Id, user, new SavePredictionsRequest
            {
                Predictions = future.Matches.Select(m => new PredictionItemRequest
                {
                    RoundMatchId = m.Id,
                    PredictedHomeScore = 1,
                    PredictedAwayScore = 0,
                }).ToList(),
            }, false, Ct));

        Assert.Contains("eliminado", ex.Message);
    }

    [Fact]
    public async Task Admin_can_reactivate_eliminated_participant()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 5; i++)
        {
            var round = await PublishedRound(db, i);
            await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        }

        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));

        await service.ReactivateAsync(user, "Reativação após acordo na liga.", null, SeedIds.AdminUser, Ct);

        Assert.False(TestSeed.IsEliminatedInDefaultGroup(db, user));
        Assert.True(TestSeed.IsActiveInDefaultGroup(db, user));
    }

    [Fact]
    public async Task Override_can_excuse_a_participant_from_absence()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await PublishedRound(db, 1);

        // No predictions => would be absent, but admin excuses them.
        await service.ApplyOverrideAsync(round.Id, new AbsenceOverrideRequest
        {
            UserId = user,
            IsAbsent = false,
            Justification = "Problema técnico comprovado.",
        }, SeedIds.AdminUser, Ct);

        Assert.False(await service.IsAbsentAsync(round.Id, user, Ct));
    }

    // --- Absence candidates offered when a participant is (re)activated -------------------

    [Fact]
    public async Task Candidate_rounds_list_only_rounds_already_closed_for_predictions()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var locked = await LockedRound(db, 1);
        var scored = await LockedRound(db, 2);
        SetStatus(db, scored.Id, RoundStatus.Scored);
        var published = await PublishedRound(db, 3);          // still accepts predictions
        var cancelled = await PublishedRound(db, 4);
        SetStatus(db, cancelled.Id, RoundStatus.Cancelled);   // never scores

        var candidates = await service.GetAbsenceCandidateRoundsAsync(user, Ct);

        Assert.Equal(new[] { locked.Id, scored.Id }, candidates.Select(c => c.RoundId));
        Assert.DoesNotContain(candidates, c => c.RoundId == published.Id || c.RoundId == cancelled.Id);
    }

    [Fact]
    public async Task Candidate_rounds_flag_scored_rounds_as_requiring_a_rescore()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var locked = await LockedRound(db, 1);
        var scored = await LockedRound(db, 2);
        SetStatus(db, scored.Id, RoundStatus.Scored);

        var candidates = await service.GetAbsenceCandidateRoundsAsync(user, Ct);

        // A Locked round's override lands on its own at scoring time; a Scored one does not.
        Assert.False(Assert.Single(candidates, c => c.RoundId == locked.Id).RequiresRescore);
        Assert.True(Assert.Single(candidates, c => c.RoundId == scored.Id).RequiresRescore);
    }

    [Fact]
    public async Task Candidate_rounds_skip_rounds_the_participant_fully_predicted()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var complete = await PublishedRound(db, 1, matchCount: 2);
        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());
        await predictions.SavePredictionsAsync(complete.Id, user, new SavePredictionsRequest
        {
            Predictions = complete.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = 1,
                PredictedAwayScore = 0,
            }).ToList(),
        }, false, Ct);
        SetStatus(db, complete.Id, RoundStatus.Locked);

        var partial = await LockedRound(db, 2, matchCount: 2);

        var candidates = await service.GetAbsenceCandidateRoundsAsync(user, Ct);

        var only = Assert.Single(candidates);
        Assert.Equal(partial.Id, only.RoundId);
        Assert.Equal(0, only.PredictionCount);
        Assert.Equal(2, only.MatchCount);
    }

    [Fact]
    public async Task Candidate_rounds_drop_rounds_already_forced_absent_but_surface_forced_present()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var forcedAbsent = await LockedRound(db, 1);
        var forcedPresent = await LockedRound(db, 2);

        await service.ApplyOverrideAsync(forcedAbsent.Id, new AbsenceOverrideRequest
        {
            UserId = user,
            IsAbsent = true,
            Justification = "Já registrado antes.",
        }, SeedIds.AdminUser, Ct);
        await service.ApplyOverrideAsync(forcedPresent.Id, new AbsenceOverrideRequest
        {
            UserId = user,
            IsAbsent = false,
            Justification = "Problema técnico comprovado.",
        }, SeedIds.AdminUser, Ct);

        var candidates = await service.GetAbsenceCandidateRoundsAsync(user, Ct);

        // Nothing to do for the one already absent; the excused one is shown, not hidden,
        // so the admin sees the conflict before replacing it.
        var only = Assert.Single(candidates);
        Assert.Equal(forcedPresent.Id, only.RoundId);
        Assert.True(only.HasPresentOverride);
    }

    [Fact]
    public async Task Candidate_rounds_do_not_leak_another_groups_rounds()
    {
        using var db = CreateContext();
        var user = CreateParticipant(db);
        await LockedRound(db, 1);

        var otherGroup = Guid.NewGuid();
        var foreignCurrent = new FakeCurrentGroupService(otherGroup);
        var foreign = new AbsenceService(
            db, new AuditService(db), foreignCurrent, TestServices.ScoringConfig(db, foreignCurrent));

        // The participant is not an approved member of the other group at all.
        await Assert.ThrowsAsync<NotFoundException>(() => foreign.GetAbsenceCandidateRoundsAsync(user, Ct));
    }

    [Fact]
    public async Task Reactivating_records_the_chosen_absences_with_the_typed_justification()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        for (var i = 1; i <= 5; i++)
        {
            var round = await PublishedRound(db, i);
            await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
        }
        Assert.True(TestSeed.IsEliminatedInDefaultGroup(db, user));

        var pending = await LockedRound(db, 6);
        await service.ReactivateAsync(
            user, "Voltou a participar.", new[] { pending.Id }, SeedIds.AdminUser, Ct);

        Assert.False(TestSeed.IsEliminatedInDefaultGroup(db, user));
        var stored = db.AbsenceOverrides.Single(o => o.RoundId == pending.Id && o.UserId == user);
        Assert.True(stored.IsAbsent);
        Assert.Equal("Voltou a participar.", stored.Justification);
    }

    [Fact]
    public async Task Duplicate_round_ids_produce_a_single_override()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await LockedRound(db, 1);

        // Without the Distinct guard the two inserts would violate the unique (RoundId, UserId) index.
        await service.StageAbsenceOverridesAsync(
            user, new[] { round.Id, round.Id }, "Ativado depois do fechamento.", SeedIds.AdminUser, Ct);
        await db.SaveChangesAsync(Ct);

        Assert.Single(db.AbsenceOverrides.Where(o => o.RoundId == round.Id && o.UserId == user));
    }

    [Fact]
    public async Task Staging_a_round_still_open_for_predictions_is_rejected()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var stillOpen = await PublishedRound(db, 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StageAbsenceOverridesAsync(
            user, new[] { stillOpen.Id }, "Ativado depois do fechamento.", SeedIds.AdminUser, Ct));
    }

    // --- Absence review: excusing (or restoring) absences in closed rounds ----------------

    private static async Task Predict(AppDbContext db, RoundDto round, Guid user)
    {
        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());
        await predictions.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest
        {
            Predictions = round.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = 1,
                PredictedAwayScore = 0,
            }).ToList(),
        }, false, Ct);
    }

    private static Task Override(AbsenceService service, Guid roundId, Guid user, bool isAbsent)
        => service.ApplyOverrideAsync(roundId, new AbsenceOverrideRequest
        {
            UserId = user,
            IsAbsent = isAbsent,
            Justification = "Ajuste manual.",
        }, SeedIds.AdminUser, Ct);

    private static AbsenceReviewDecision Decide(Guid roundId, bool isAbsent)
        => new() { RoundId = roundId, IsAbsent = isAbsent };

    [Fact]
    public async Task Review_rounds_list_closed_rounds_with_incomplete_predictions_or_an_override()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var lockedAbsent = await LockedRound(db, 1);
        var scoredAbsent = await LockedRound(db, 2);
        SetStatus(db, scoredAbsent.Id, RoundStatus.Scored);
        var forcedAbsent = await PublishedRound(db, 3);          // complete, but forced absent
        await Predict(db, forcedAbsent, user);
        await Override(service, forcedAbsent.Id, user, isAbsent: true);
        SetStatus(db, forcedAbsent.Id, RoundStatus.Locked);
        var excused = await LockedRound(db, 4);                   // no predictions, but excused
        await Override(service, excused.Id, user, isAbsent: false);
        var complete = await PublishedRound(db, 5);              // nothing to review
        await Predict(db, complete, user);
        SetStatus(db, complete.Id, RoundStatus.Locked);
        var stillOpen = await PublishedRound(db, 6);
        var cancelled = await PublishedRound(db, 7);
        SetStatus(db, cancelled.Id, RoundStatus.Cancelled);

        var rounds = await service.GetAbsenceReviewRoundsAsync(user, Ct);

        Assert.Equal(
            new[] { lockedAbsent.Id, scoredAbsent.Id, forcedAbsent.Id, excused.Id },
            rounds.Select(r => r.RoundId));
        Assert.DoesNotContain(rounds, r => r.RoundId == complete.Id || r.RoundId == stillOpen.Id || r.RoundId == cancelled.Id);

        var locked = rounds.Single(r => r.RoundId == lockedAbsent.Id);
        Assert.True(locked.IsAbsent);
        Assert.False(locked.HasOverride);
        Assert.False(locked.RequiresRecalculation);

        var scored = rounds.Single(r => r.RoundId == scoredAbsent.Id);
        Assert.True(scored.IsAbsent);
        Assert.True(scored.RequiresRecalculation);

        var forced = rounds.Single(r => r.RoundId == forcedAbsent.Id);
        Assert.True(forced.IsAbsent);
        Assert.True(forced.HasOverride);
        Assert.Equal(forced.MatchCount, forced.PredictionCount);

        var present = rounds.Single(r => r.RoundId == excused.Id);
        Assert.False(present.IsAbsent);
        Assert.True(present.HasOverride);
    }

    [Fact]
    public async Task Review_rounds_expose_the_recorded_ordinal_and_penalty()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);

        var ids = new List<Guid>();
        foreach (var number in new[] { 1, 2, 3 })
        {
            var round = await PublishedRound(db, number);
            await service.ProcessRoundAbsencesAsync(round.Id, SeedIds.AdminUser, Ct);
            SetStatus(db, round.Id, RoundStatus.Scored);
            ids.Add(round.Id);
        }

        var rounds = await service.GetAbsenceReviewRoundsAsync(user, Ct);

        var first = Assert.Single(rounds, r => r.RoundId == ids[0]);
        Assert.Equal(1, first.AbsenceNumber);
        Assert.Equal(0, first.PenaltyPoints);
        var third = Assert.Single(rounds, r => r.RoundId == ids[2]);
        Assert.Equal(3, third.AbsenceNumber);
        Assert.Equal(20, third.PenaltyPoints);
    }

    [Fact]
    public async Task Review_rounds_show_no_ordinal_for_a_round_not_yet_scored()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var locked = await LockedRound(db, 1);

        var only = Assert.Single(await service.GetAbsenceReviewRoundsAsync(user, Ct));

        Assert.Equal(locked.Id, only.RoundId);
        Assert.Null(only.AbsenceNumber);
        Assert.Null(only.PenaltyPoints);
    }

    [Fact]
    public async Task Staging_a_review_writes_overrides_only_for_rounds_whose_state_changes()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var kept = await LockedRound(db, 1);
        var excused = await LockedRound(db, 2);

        var staged = await service.StageAbsenceReviewAsync(
            user, new[] { Decide(kept.Id, true), Decide(excused.Id, false) },
            "Ainda não participava.", SeedIds.AdminUser, Ct);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(new[] { excused.Id }, staged.ChangedRoundIds);
        Assert.Equal(SeasonId, staged.SeasonId);
        var stored = Assert.Single(db.AbsenceOverrides.Where(o => o.UserId == user));
        Assert.Equal(excused.Id, stored.RoundId);
        Assert.False(stored.IsAbsent);
        Assert.Equal("Ainda não participava.", stored.Justification);
        Assert.Single(db.AuditLogs.Where(a => a.Action == "AbsenceOverride"));
    }

    [Fact]
    public async Task Staging_a_review_flags_recalculation_only_when_a_scored_round_changes()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var locked = await LockedRound(db, 1);
        var scored = await LockedRound(db, 2);
        SetStatus(db, scored.Id, RoundStatus.Scored);

        var lockedOnly = await service.StageAbsenceReviewAsync(
            user, new[] { Decide(locked.Id, false) }, "Ainda não participava.", SeedIds.AdminUser, Ct);
        await db.SaveChangesAsync(Ct);
        var scoredToo = await service.StageAbsenceReviewAsync(
            user, new[] { Decide(scored.Id, false) }, "Ainda não participava.", SeedIds.AdminUser, Ct);

        // A Locked round's override lands on its own at scoring time; a Scored one needs the replay.
        Assert.False(lockedOnly.RequiresRecalculation);
        Assert.True(scoredToo.RequiresRecalculation);
    }

    [Fact]
    public async Task Staging_a_review_re_marks_an_excused_round_absent()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await LockedRound(db, 1);
        await Override(service, round.Id, user, isAbsent: false);

        var staged = await service.StageAbsenceReviewAsync(
            user, new[] { Decide(round.Id, true) }, "Abono concedido por engano.", SeedIds.AdminUser, Ct);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(new[] { round.Id }, staged.ChangedRoundIds);
        var stored = Assert.Single(db.AbsenceOverrides.Where(o => o.RoundId == round.Id && o.UserId == user));
        Assert.True(stored.IsAbsent);
        Assert.Equal("Abono concedido por engano.", stored.Justification);
    }

    [Fact]
    public async Task Staging_a_review_with_no_changes_stages_nothing()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var absent = await LockedRound(db, 1);
        var excused = await LockedRound(db, 2);
        await Override(service, excused.Id, user, isAbsent: false);

        // Every decision matches today's state, override included.
        var staged = await service.StageAbsenceReviewAsync(
            user, new[] { Decide(absent.Id, true), Decide(excused.Id, false) },
            "Conferido.", SeedIds.AdminUser, Ct);
        await db.SaveChangesAsync(Ct);

        Assert.Empty(staged.ChangedRoundIds);
        Assert.False(staged.RequiresRecalculation);
        Assert.Single(db.AbsenceOverrides);
        Assert.Single(db.AuditLogs.Where(a => a.Action == "AbsenceOverride"));
    }

    [Fact]
    public async Task Staging_a_review_rejects_a_round_outside_the_review_set()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var stillOpen = await PublishedRound(db, 1);
        var complete = await PublishedRound(db, 2);
        await Predict(db, complete, user);
        SetStatus(db, complete.Id, RoundStatus.Locked);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StageAbsenceReviewAsync(
            user, new[] { Decide(stillOpen.Id, false) }, "Ainda não participava.", SeedIds.AdminUser, Ct));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StageAbsenceReviewAsync(
            user, new[] { Decide(complete.Id, false) }, "Ainda não participava.", SeedIds.AdminUser, Ct));

        Assert.Empty(db.AbsenceOverrides);
    }

    [Fact]
    public async Task Staging_a_review_requires_a_justification()
    {
        using var db = CreateContext();
        var service = Service(db);
        var user = CreateParticipant(db);
        var round = await LockedRound(db, 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StageAbsenceReviewAsync(
            user, new[] { Decide(round.Id, false) }, "  ", SeedIds.AdminUser, Ct));
    }

    [Fact]
    public async Task Review_rounds_do_not_leak_another_groups_rounds()
    {
        using var db = CreateContext();
        var user = CreateParticipant(db);
        await LockedRound(db, 1);

        var otherGroup = Guid.NewGuid();
        var foreignCurrent = new FakeCurrentGroupService(otherGroup);
        var foreign = new AbsenceService(
            db, new AuditService(db), foreignCurrent, TestServices.ScoringConfig(db, foreignCurrent));

        // The participant is not an approved member of the other group at all.
        await Assert.ThrowsAsync<NotFoundException>(() => foreign.GetAbsenceReviewRoundsAsync(user, Ct));
    }
}

