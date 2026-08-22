using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Standings;

/// <summary>
/// The temporary standings list the whole active roster, so someone who has not predicted shows
/// up on zero and — once predictions have closed — carries the "will be absent" flag.
/// </summary>
public class TemporaryStandingsServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;

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

    private static Guid CreateParticipant(AppDbContext db, string name, bool isActive = true)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"user-{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        TestSeed.AddDefaultGroupMembership(db, id, isActive: isActive);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> PublishedRound(AppDbContext db, DateTime firstKickoff)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService(), TestServices.ScoringConfig(db));
        var round = await rounds.CreateAsync(new CreateRoundRequest { SeasonId = SeasonId, Number = 1 }, Admin, Ct);
        await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
        {
            Competition = Competition.Championship,
            Phase = MatchPhase.Regular,
            HomeTeamId = SeedIds.Arsenal,
            AwayTeamId = SeedIds.Chelsea,
            StartsAt = firstKickoff,
        }, Admin, Ct);
        return await rounds.PublishAsync(round.Id, Admin, Ct);
    }

    /// <summary>An open round: predictions can still be saved (kickoff is ahead).</summary>
    private static Task<RoundDto> OpenRound(AppDbContext db) => PublishedRound(db, DateTime.UtcNow.AddHours(3));

    /// <summary>
    /// Kicks the round off: moves the match into the past (so the general prediction deadline
    /// has passed) and puts it live with a 1-0. Call it <b>after</b> saving any predictions —
    /// PredictionsService rejects a save once the deadline is behind us.
    /// </summary>
    private static async Task GoLive(AppDbContext db, RoundDto round)
    {
        var started = DateTime.UtcNow.AddHours(-2);
        var match = await db.RoundMatches.FirstAsync(m => m.RoundId == round.Id, Ct);
        match.StartsAt = started;
        match.Status = MatchStatus.InProgress;
        match.HomeScore = 1;
        match.AwayScore = 0;
        match.LastResultUpdatedAt = DateTime.UtcNow;
        // Drives Round.PredictionDeadlineUtc, which is what the absent flag is gated on.
        (await db.Rounds.FirstAsync(r => r.Id == round.Id, Ct)).FirstMatchStartsAt = started;
        await db.SaveChangesAsync(Ct);
    }

    private static async Task Predict(AppDbContext db, RoundDto round, Guid user, int home, int away)
    {
        var predictions = new PredictionsService(db, new AuditService(db), new FakeCurrentGroupService());
        await predictions.SavePredictionsAsync(round.Id, user, new SavePredictionsRequest
        {
            Predictions = round.Matches.Select(m => new PredictionItemRequest
            {
                RoundMatchId = m.Id,
                PredictedHomeScore = home,
                PredictedAwayScore = away,
            }).ToList(),
        }, false, Ct);
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Participant_without_predictions_is_listed_on_zero_and_flagged_absent()
    {
        using var db = CreateContext();
        var predictor = CreateParticipant(db, "Bruno");
        var silent = CreateParticipant(db, "João Paulo");
        var round = await OpenRound(db);
        await Predict(db, round, predictor, 1, 0);
        await GoLive(db, round);

        var result = await TestServices.TemporaryStandings(db).GetTemporaryStandingsAsync(round.Id, Ct);

        // Before this change the silent participant was missing from the list entirely.
        var row = Assert.Single(result.Standings, s => s.UserId == silent);
        Assert.Equal(0, row.RoundTemporaryPoints);
        Assert.True(row.WillBeAbsent);
        Assert.False(Assert.Single(result.Standings, s => s.UserId == predictor).WillBeAbsent);
    }

    [Fact]
    public async Task Nobody_is_flagged_while_predictions_are_still_open()
    {
        using var db = CreateContext();
        var silent = CreateParticipant(db, "João Paulo");
        // Kickoff still ahead: the deadline has not passed, so they can still submit.
        var round = await PublishedRound(db, DateTime.UtcNow.AddHours(3));
        var match = await db.RoundMatches.FirstAsync(m => m.RoundId == round.Id, Ct);
        match.Status = MatchStatus.Finished;
        match.HomeScore = 2;
        match.AwayScore = 1;
        match.IsFinished = true;
        await db.SaveChangesAsync(Ct);

        var result = await TestServices.TemporaryStandings(db).GetTemporaryStandingsAsync(round.Id, Ct);

        var row = Assert.Single(result.Standings, s => s.UserId == silent);
        Assert.Equal(0, row.RoundTemporaryPoints);
        Assert.False(row.WillBeAbsent);
    }

    [Fact]
    public async Task Participant_excused_by_an_override_is_listed_but_not_flagged()
    {
        using var db = CreateContext();
        var silent = CreateParticipant(db, "João Paulo");
        var round = await OpenRound(db);
        await GoLive(db, round);

        await TestServices.Absences(db).ApplyOverrideAsync(round.Id, new AbsenceOverrideRequest
        {
            UserId = silent,
            IsAbsent = false,
            Justification = "Problema técnico comprovado.",
        }, Admin, Ct);

        var result = await TestServices.TemporaryStandings(db).GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.False(Assert.Single(result.Standings, s => s.UserId == silent).WillBeAbsent);
    }

    [Fact]
    public async Task Override_flags_a_participant_who_predicted_without_zeroing_their_points()
    {
        using var db = CreateContext();
        var user = CreateParticipant(db, "Bruno");
        var round = await OpenRound(db);
        await Predict(db, round, user, 1, 0); // exact hit on the live 1-0
        await GoLive(db, round);

        await TestServices.Absences(db).ApplyOverrideAsync(round.Id, new AbsenceOverrideRequest
        {
            UserId = user,
            IsAbsent = true,
            Justification = "Ativado depois do fechamento da rodada.",
        }, Admin, Ct);

        var result = await TestServices.TemporaryStandings(db).GetTemporaryStandingsAsync(round.Id, Ct);

        // Flagging is an annotation: the temporary standings still never apply the absence.
        var row = Assert.Single(result.Standings, s => s.UserId == user);
        Assert.True(row.WillBeAbsent);
        Assert.True(row.RoundTemporaryPoints > 0);
    }

    [Fact]
    public async Task Inactive_participants_stay_out_of_the_list()
    {
        using var db = CreateContext();
        var active = CreateParticipant(db, "Bruno");
        var inactive = CreateParticipant(db, "Fora", isActive: false);
        var round = await OpenRound(db);
        await Predict(db, round, active, 1, 0);
        await GoLive(db, round);

        var result = await TestServices.TemporaryStandings(db).GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.DoesNotContain(result.Standings, s => s.UserId == inactive);
    }

    [Fact]
    public async Task An_absentee_ranks_below_a_present_participant_on_the_same_points()
    {
        using var db = CreateContext();
        // "Ana" sorts before "Zeca" by name, so only the absent flag can put her last.
        var absent = CreateParticipant(db, "Ana");
        var present = CreateParticipant(db, "Zeca");
        var round = await OpenRound(db);
        await Predict(db, round, present, 5, 5); // wrong on every column -> 0 points too
        await GoLive(db, round);

        var result = await TestServices.TemporaryStandings(db).GetTemporaryStandingsAsync(round.Id, Ct);

        Assert.Equal(0, Assert.Single(result.Standings, s => s.UserId == present).RoundTemporaryPoints);
        Assert.Equal(new[] { present, absent }, result.Standings.Select(s => s.UserId));
    }
}
