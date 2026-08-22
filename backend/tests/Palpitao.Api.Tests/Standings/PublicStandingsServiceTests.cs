using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Standings;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Standings;

/// <summary>
/// The public link has no session and no <c>X-Group-Id</c> to lean on, and the EF global
/// filter is inert without a request group — it matches every group rather than none. These
/// tests therefore treat cross-tenant leakage as the primary risk, alongside the rule that a
/// round which has not closed must never be visible.
/// </summary>
public class PublicStandingsServiceTests
{
    private const string KeyA = "AAAA1111BBBB";
    private const string KeyB = "CCCC2222DDDD";

    private static readonly Guid GroupA = SeedIds.DefaultGroup;
    private static readonly Guid GroupB = Guid.Parse("33333333-3333-3333-3333-3333333333B0");
    private static readonly Guid SeasonA = Guid.Parse("44444444-4444-4444-4444-4444444444A0");
    private static readonly Guid SeasonB = Guid.Parse("44444444-4444-4444-4444-4444444444B0");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        TestSeed.AddNeutralTeams(db);

        db.Groups.Add(new Group
        {
            Id = GroupB,
            Name = "Other group",
            Slug = "other-group",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.Seasons.Add(NewSeason(SeasonA, GroupA, "Season A", KeyA));
        db.Seasons.Add(NewSeason(SeasonB, GroupB, "Season B", KeyB));
        db.SaveChanges();
        return db;
    }

    private static Season NewSeason(Guid id, Guid groupId, string name, string key) => new()
    {
        Id = id,
        GroupId = groupId,
        Name = name,
        PublicKey = key,
        PublicStandingsEnabled = true,
        StartDate = new DateOnly(2025, 8, 1),
        EndDate = new DateOnly(2026, 5, 31),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static PublicStandingsService Build(AppDbContext db)
        => new(db, new ScoringService(), TestServices.ScoringConfig(db));

    private static Guid AddParticipant(AppDbContext db, Guid groupId, string name)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"{id}@palpitao.local",
            PasswordHash = "x",
            Role = UserRole.Participant,
            Status = UserStatus.Approved,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = id,
            Role = GroupRole.Participant,
            Status = GroupUserStatus.Approved,
            IsActive = true,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        return id;
    }

    private static Round AddRound(AppDbContext db, Guid seasonId, Guid groupId, int number, RoundStatus status)
    {
        var round = new Round
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            GroupId = groupId,
            Number = number,
            Status = status,
            CreatedByUserId = SeedIds.AdminUser,
            CreatedAt = DateTime.UtcNow,
        };
        db.Rounds.Add(round);
        return round;
    }

    private static RoundMatch AddMatch(AppDbContext db, Round round, int order, int home, int away)
    {
        var (homeTeam, awayTeam) = TestSeed.NeutralPairs[order];
        var match = new RoundMatch
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            Competition = Competition.PremierLeague,
            Phase = MatchPhase.Regular,
            HomeTeamId = homeTeam,
            AwayTeamId = awayTeam,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            Order = order,
            HomeScore = home,
            AwayScore = away,
            IsFinished = true,
            Status = MatchStatus.Finished,
            CreatedAt = DateTime.UtcNow,
        };
        db.RoundMatches.Add(match);
        return match;
    }

    private static void AddPrediction(AppDbContext db, Round round, RoundMatch match, Guid userId, int home, int away)
        => db.Predictions.Add(new Prediction
        {
            Id = Guid.NewGuid(),
            RoundId = round.Id,
            RoundMatchId = match.Id,
            UserId = userId,
            PredictedHomeScore = home,
            PredictedAwayScore = away,
            SubmittedAt = DateTime.UtcNow.AddDays(-2),
        });

    private static void AddResult(
        AppDbContext db, Round round, Guid userId, int points, bool absent = false, bool flavio = false)
        => db.RoundParticipantResults.Add(new RoundParticipantResult
        {
            Id = Guid.NewGuid(),
            GroupId = round.GroupId,
            SeasonId = round.SeasonId,
            RoundId = round.Id,
            UserId = userId,
            GrossPoints = points,
            FinalPoints = points,
            WasAbsent = absent,
            FlavioRuleApplied = flavio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

    // ---------------------------------------------------------------------
    // Key resolution
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(KeyA)]
    [InlineData("aaaa1111bbbb")]
    [InlineData("AAAA-1111-BBBB")]
    public async Task Resolves_a_key_in_any_cosmetic_form(string key)
    {
        using var db = CreateContext();

        var season = await Build(db).GetSeasonAsync(key, Ct);

        Assert.Equal("Season A", season.SeasonName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-key")]
    [InlineData("FFFFFFFFFFFF")]
    public async Task Rejects_a_key_that_resolves_to_nothing(string key)
    {
        using var db = CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => Build(db).GetSeasonAsync(key, Ct));
    }

    [Fact]
    public async Task An_unpublished_season_is_indistinguishable_from_a_missing_one()
    {
        using var db = CreateContext();
        db.Seasons.Single(s => s.Id == SeasonA).PublicStandingsEnabled = false;
        db.SaveChanges();

        // Same exception as an unknown key: probing must not reveal that the season exists.
        await Assert.ThrowsAsync<NotFoundException>(() => Build(db).GetSeasonAsync(KeyA, Ct));
    }

    // ---------------------------------------------------------------------
    // Tenant isolation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_key_never_reaches_another_groups_rounds()
    {
        using var db = CreateContext();
        AddRound(db, SeasonA, GroupA, 1, RoundStatus.Scored);
        AddRound(db, SeasonB, GroupB, 2, RoundStatus.Scored);
        db.SaveChanges();

        var season = await Build(db).GetSeasonAsync(KeyA, Ct);

        Assert.Equal(new[] { 1 }, season.Rounds.Select(r => r.Number).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() => Build(db).GetRoundAsync(KeyA, 2, Ct));
    }

    [Fact]
    public async Task A_key_never_reaches_another_groups_standings()
    {
        using var db = CreateContext();
        var mine = AddParticipant(db, GroupA, "Mine");
        var theirs = AddParticipant(db, GroupB, "Theirs");
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(), GroupId = GroupA, SeasonId = SeasonA, UserId = mine,
            TotalPoints = 10, Position = 1, UpdatedAt = DateTime.UtcNow,
        });
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(), GroupId = GroupB, SeasonId = SeasonB, UserId = theirs,
            TotalPoints = 99, Position = 1, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var rows = await Build(db).GetStandingsAsync(KeyA, Ct);

        Assert.Equal(new[] { "Mine" }, rows.Select(r => r.Name).ToArray());
    }

    // ---------------------------------------------------------------------
    // Only closed rounds are visible
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(RoundStatus.Draft)]
    [InlineData(RoundStatus.Published)]
    [InlineData(RoundStatus.Cancelled)]
    public async Task An_open_round_is_invisible(RoundStatus status)
    {
        using var db = CreateContext();
        AddRound(db, SeasonA, GroupA, 7, status);
        db.SaveChanges();

        var season = await Build(db).GetSeasonAsync(KeyA, Ct);

        Assert.Empty(season.Rounds);
        // A prediction is still copyable while the round is open, so the breakdown is denied
        // outright rather than merely hidden from the list.
        await Assert.ThrowsAsync<NotFoundException>(() => Build(db).GetRoundAsync(KeyA, 7, Ct));
    }

    [Theory]
    [InlineData(RoundStatus.Locked)]
    [InlineData(RoundStatus.Scored)]
    public async Task A_closed_round_is_visible(RoundStatus status)
    {
        using var db = CreateContext();
        AddRound(db, SeasonA, GroupA, 7, status);
        db.SaveChanges();

        var season = await Build(db).GetSeasonAsync(KeyA, Ct);

        Assert.Equal(7, Assert.Single(season.Rounds).Number);
    }

    // ---------------------------------------------------------------------
    // The audit itself
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_scored_round_reports_the_persisted_breakdown_not_a_recomputation()
    {
        using var db = CreateContext();
        var user = AddParticipant(db, GroupA, "Ana");
        var round = AddRound(db, SeasonA, GroupA, 3, RoundStatus.Scored);
        var match = AddMatch(db, round, 0, 2, 1);
        AddPrediction(db, round, match, user, 2, 1);
        db.PredictionScores.Add(new PredictionScore
        {
            Id = Guid.NewGuid(), RoundId = round.Id, RoundMatchId = match.Id, UserId = user,
            PredictionId = Guid.NewGuid(),
            BasePoints = 3, Multiplier = 2, FinalPoints = 6,
            ScoreCategory = ScoreCategory.Traditional, IsExactScore = true, IsCorrectColumn = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.RoundParticipantResults.Add(new RoundParticipantResult
        {
            Id = Guid.NewGuid(), GroupId = GroupA, SeasonId = SeasonA, RoundId = round.Id, UserId = user,
            GrossPoints = 12, FinalPoints = 6, FlavioRuleApplied = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var dto = await Build(db).GetRoundAsync(KeyA, 3, Ct);

        Assert.False(dto.IsPartial);
        var participant = Assert.Single(dto.Participants);
        // The Flávio halving lives only in the persisted result; recomputing would hide it.
        Assert.Equal(12, participant.GrossPoints);
        Assert.Equal(6, participant.FinalPoints);
        Assert.True(participant.FlavioRuleApplied);

        var score = Assert.Single(participant.MatchScores);
        Assert.Equal(3, score.BasePoints);
        Assert.Equal(2, score.Multiplier);
        Assert.Equal(6, score.FinalPoints);
        Assert.Equal(ScoreCategory.Traditional, score.ScoreCategory);
        // Without the prediction beside the points there is nothing to audit.
        Assert.Equal(2, score.PredictedHomeScore);
        Assert.Equal(1, score.PredictedAwayScore);
    }

    [Fact]
    public async Task A_locked_round_is_computed_live_and_flagged_as_partial()
    {
        using var db = CreateContext();
        var user = AddParticipant(db, GroupA, "Ana");
        var round = AddRound(db, SeasonA, GroupA, 4, RoundStatus.Locked);
        var match = AddMatch(db, round, 0, 2, 1);
        AddPrediction(db, round, match, user, 2, 1);
        db.SaveChanges();

        var dto = await Build(db).GetRoundAsync(KeyA, 4, Ct);

        Assert.True(dto.IsPartial);
        Assert.Equal(1, dto.ComputedMatches);
        var participant = Assert.Single(dto.Participants);
        var score = Assert.Single(participant.MatchScores);
        // 2x1 exactly: Traditional, 3 base points, plain multiplier on a neutral pair.
        Assert.Equal(ScoreCategory.Traditional, score.ScoreCategory);
        Assert.Equal(3, score.BasePoints);
        Assert.Equal(3, score.FinalPoints);
        Assert.True(score.IsExactScore);
        Assert.Equal(2, score.PredictedHomeScore);
    }

    [Fact]
    public async Task A_manual_multiplier_override_wins_on_the_live_path()
    {
        using var db = CreateContext();
        var user = AddParticipant(db, GroupA, "Ana");
        var round = AddRound(db, SeasonA, GroupA, 5, RoundStatus.Locked);
        var match = AddMatch(db, round, 0, 2, 1);
        match.ManualMultiplierOverride = 5;
        match.ManualMultiplierJustification = "cup final";
        AddPrediction(db, round, match, user, 2, 1);
        db.SaveChanges();

        var dto = await Build(db).GetRoundAsync(KeyA, 5, Ct);

        var score = Assert.Single(Assert.Single(dto.Participants).MatchScores);
        Assert.Equal(5, score.Multiplier);
        Assert.Equal(15, score.FinalPoints);
        Assert.True(Assert.Single(dto.Matches).IsManualMultiplier);
    }

    [Fact]
    public async Task The_ruleset_comes_from_the_season_so_the_page_can_explain_itself()
    {
        using var db = CreateContext();

        var season = await Build(db).GetSeasonAsync(KeyA, Ct);

        Assert.Equal(1, season.Ruleset.ColumnOnly);
        Assert.Equal(3, season.Ruleset.Traditional);
        Assert.Equal(5, season.Ruleset.Medium);
        Assert.Equal(7, season.Ruleset.Uncommon);
        Assert.Equal(10, season.Ruleset.ExtraUncommon);
    }

    [Fact]
    public async Task Reports_the_group_that_owns_the_season()
    {
        using var db = CreateContext();

        var season = await Build(db).GetSeasonAsync(KeyB, Ct);

        Assert.Equal("Other group", season.GroupName);
        Assert.Equal("Season B", season.SeasonName);
    }

    // ---------------------------------------------------------------------
    // Round-by-round history on the standings rows
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_standings_row_carries_its_scored_rounds_oldest_first()
    {
        using var db = CreateContext();
        var user = AddParticipant(db, GroupA, "Mine");
        var first = AddRound(db, SeasonA, GroupA, 1, RoundStatus.Scored);
        var second = AddRound(db, SeasonA, GroupA, 2, RoundStatus.Scored);
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(), GroupId = GroupA, SeasonId = SeasonA, UserId = user,
            TotalPoints = 30, Position = 1, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        // Inserted newest-first on purpose: the ordering must come from the round number.
        AddResult(db, second, user, 18, flavio: true);
        AddResult(db, first, user, 12);
        db.SaveChanges();

        var row = Assert.Single(await Build(db).GetStandingsAsync(KeyA, Ct));

        Assert.Equal(new[] { 1, 2 }, row.Rounds.Select(r => r.Number).ToArray());
        Assert.Equal(new[] { 12, 18 }, row.Rounds.Select(r => r.Points).ToArray());
        Assert.True(row.Rounds[1].FlavioRuleApplied);
        Assert.False(row.Rounds[0].FlavioRuleApplied);
    }

    [Fact]
    public async Task The_history_ignores_rounds_that_are_not_scored()
    {
        using var db = CreateContext();
        var user = AddParticipant(db, GroupA, "Mine");
        var scored = AddRound(db, SeasonA, GroupA, 1, RoundStatus.Scored);
        var locked = AddRound(db, SeasonA, GroupA, 2, RoundStatus.Locked);
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(), GroupId = GroupA, SeasonId = SeasonA, UserId = user,
            TotalPoints = 12, Position = 1, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        AddResult(db, scored, user, 12);
        // A stray result on a round that was reopened must not surface as official history.
        AddResult(db, locked, user, 99);
        db.SaveChanges();

        var row = Assert.Single(await Build(db).GetStandingsAsync(KeyA, Ct));

        Assert.Equal(new[] { 1 }, row.Rounds.Select(r => r.Number).ToArray());
    }

    [Fact]
    public async Task A_participant_with_no_result_gets_an_empty_history_not_a_missing_row()
    {
        using var db = CreateContext();
        var user = AddParticipant(db, GroupA, "Mine");
        AddRound(db, SeasonA, GroupA, 1, RoundStatus.Scored);
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(), GroupId = GroupA, SeasonId = SeasonA, UserId = user,
            TotalPoints = 0, Position = 1, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var row = Assert.Single(await Build(db).GetStandingsAsync(KeyA, Ct));

        Assert.Empty(row.Rounds);
    }

    [Fact]
    public async Task The_history_never_reaches_another_groups_results()
    {
        using var db = CreateContext();
        var mine = AddParticipant(db, GroupA, "Mine");
        var mineRound = AddRound(db, SeasonA, GroupA, 1, RoundStatus.Scored);
        // Same round number in the neighbouring group, with a very different score.
        var theirRound = AddRound(db, SeasonB, GroupB, 1, RoundStatus.Scored);
        db.Standings.Add(new Standing
        {
            Id = Guid.NewGuid(), GroupId = GroupA, SeasonId = SeasonA, UserId = mine,
            TotalPoints = 12, Position = 1, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        AddResult(db, mineRound, mine, 12);
        AddResult(db, theirRound, mine, 99);
        db.SaveChanges();

        var row = Assert.Single(await Build(db).GetStandingsAsync(KeyA, Ct));

        Assert.Equal(new[] { 12 }, row.Rounds.Select(r => r.Points).ToArray());
    }
}
