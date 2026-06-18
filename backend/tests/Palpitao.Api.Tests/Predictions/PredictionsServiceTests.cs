using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
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

namespace Palpitao.Api.Tests.Predictions;

public class PredictionsServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly (Guid Home, Guid Away)[] Pairs =
    {
        (SeedIds.Arsenal, SeedIds.Chelsea),
        (SeedIds.Liverpool, SeedIds.Newcastle),
        (SeedIds.Tottenham, SeedIds.ManchesterCity),
    };

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

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

    private static PredictionsService CreateService(AppDbContext db) => new(db, new AuditService(db), new FakeCurrentGroupService());

    private static Guid CreateParticipant(AppDbContext db, bool active = true, bool eliminated = false)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = $"Participante {id.ToString()[..4]}",
            Email = $"user-{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        // Active/eliminated are per-group now: carry them on the membership.
        TestSeed.AddDefaultGroupMembership(db, id, isActive: active, isEliminated: eliminated);
        db.SaveChanges();
        return id;
    }

    private static async Task<RoundDto> CreateRoundWithMatches(
        AppDbContext db, DateTime firstStartsAt, int matchCount = 2, int number = 1)
    {
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService());
        var round = await rounds.CreateAsync(
            new CreateRoundRequest { SeasonId = SeasonId, Number = number }, SeedIds.AdminUser, Ct);

        for (var i = 0; i < matchCount; i++)
        {
            await rounds.AddMatchAsync(round.Id, new CreateMatchRequest
            {
                Competition = Competition.PremierLeague,
                Phase = MatchPhase.Regular,
                HomeTeamId = Pairs[i].Home,
                AwayTeamId = Pairs[i].Away,
                StartsAt = firstStartsAt.AddHours(i),
            }, SeedIds.AdminUser, Ct);
        }

        return await rounds.GetByIdAsync(round.Id, Ct);
    }

    private static async Task<RoundDto> PublishedRound(
        AppDbContext db, DateTime firstStartsAt, int matchCount = 2, int number = 1)
    {
        var round = await CreateRoundWithMatches(db, firstStartsAt, matchCount, number);
        var rounds = new RoundService(db, new AuditService(db), new FakeCurrentGroupService());
        return await rounds.PublishAsync(round.Id, SeedIds.AdminUser, Ct);
    }

    private static SavePredictionsRequest FullBatch(RoundDto round, int home = 1, int away = 0)
        => new()
        {
            Predictions = round.Matches
                .Select(m => new PredictionItemRequest
                {
                    RoundMatchId = m.Id,
                    PredictedHomeScore = home,
                    PredictedAwayScore = away,
                })
                .ToList(),
        };

    private static DateTime Future => DateTime.UtcNow.AddDays(2);

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Participant_can_save_predictions_in_published_round()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db);

        var result = await service.SavePredictionsAsync(round.Id, user, FullBatch(round), isEdit: false, Ct);

        Assert.Equal(round.Matches.Count, result.Predictions.Count);
    }

    [Fact]
    public async Task Participant_can_edit_predictions_before_deadline()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db);

        await service.SavePredictionsAsync(round.Id, user, FullBatch(round, 1, 0), isEdit: false, Ct);
        var edited = await service.SavePredictionsAsync(round.Id, user, FullBatch(round, 3, 2), isEdit: true, Ct);

        Assert.All(edited.Predictions, p =>
        {
            Assert.Equal(3, p.PredictedHomeScore);
            Assert.Equal(2, p.PredictedAwayScore);
            Assert.NotNull(p.UpdatedAt);
        });
    }

    [Fact]
    public async Task Cannot_predict_in_draft_round()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await CreateRoundWithMatches(db, Future); // not published
        var user = CreateParticipant(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, FullBatch(round), false, Ct));

        Assert.Contains("não está aberta", ex.Message);
    }

    [Fact]
    public async Task Cannot_predict_in_locked_round()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db);
        await new RoundService(db, new AuditService(db), new FakeCurrentGroupService()).LockAsync(round.Id, SeedIds.AdminUser, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, FullBatch(round), false, Ct));

        Assert.Contains("bloqueada", ex.Message);
    }

    [Fact]
    public async Task Cannot_predict_in_cancelled_round()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db);
        await new RoundService(db, new AuditService(db), new FakeCurrentGroupService()).CancelAsync(round.Id, SeedIds.AdminUser, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, FullBatch(round), false, Ct));

        Assert.Contains("cancelada", ex.Message);
    }

    [Fact]
    public async Task Cannot_predict_after_first_match_starts()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, DateTime.UtcNow.AddMinutes(-5)); // deadline already passed
        var user = CreateParticipant(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, FullBatch(round), false, Ct));

        Assert.Contains("prazo", ex.Message);
    }

    [Fact]
    public async Task Cannot_save_negative_score()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db);

        var batch = FullBatch(round);
        batch.Predictions[0].PredictedHomeScore = -1;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, batch, false, Ct));

        Assert.Contains("negativo", ex.Message);
    }

    [Fact]
    public async Task Cannot_save_incomplete_predictions()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future, matchCount: 2);
        var user = CreateParticipant(db);

        var batch = FullBatch(round);
        batch.Predictions.RemoveAt(0); // only one of two matches

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, batch, false, Ct));

        Assert.Contains("todos os jogos", ex.Message);
    }

    [Fact]
    public async Task Cannot_save_duplicated_matches()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future, matchCount: 2);
        var user = CreateParticipant(db);

        var batch = FullBatch(round);
        batch.Predictions[1].RoundMatchId = batch.Predictions[0].RoundMatchId; // duplicate

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, batch, false, Ct));

        Assert.Contains("duplicados", ex.Message);
    }

    [Fact]
    public async Task Cannot_save_match_not_in_round()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future, matchCount: 2);
        var user = CreateParticipant(db);

        var batch = FullBatch(round);
        batch.Predictions[1].RoundMatchId = Guid.NewGuid(); // foreign match

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, batch, false, Ct));

        Assert.Contains("não pertence", ex.Message);
    }

    [Fact]
    public async Task Eliminated_participant_cannot_predict()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db, eliminated: true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SavePredictionsAsync(round.Id, user, FullBatch(round), false, Ct));

        Assert.Contains("eliminado", ex.Message);
    }

    [Fact]
    public async Task Mirror_is_hidden_before_lock()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var viewer = CreateParticipant(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.GetMirrorAsync(round.Id, viewer, Ct));

        Assert.Contains("após o bloqueio", ex.Message);
    }

    [Fact]
    public async Task Mirror_is_visible_after_lock()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var round = await PublishedRound(db, Future);
        var user = CreateParticipant(db);

        await service.SavePredictionsAsync(round.Id, user, FullBatch(round), false, Ct);
        await new RoundService(db, new AuditService(db), new FakeCurrentGroupService()).LockAsync(round.Id, SeedIds.AdminUser, Ct);

        var mirror = await service.GetMirrorAsync(round.Id, user, Ct);

        Assert.Equal(round.Matches.Count, mirror.Matches.Count);
        var participant = Assert.Single(mirror.Participants, p => p.UserId == user);
        Assert.False(participant.IsAbsent);
        Assert.Equal(round.Matches.Count, participant.Predictions.Count);
    }
}
