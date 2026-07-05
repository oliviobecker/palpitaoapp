using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Ocr;
using Palpitao.Api.Tests.TestSupport;
using Xunit;

namespace Palpitao.Api.Tests.Ocr;

public class PredictionImportServiceTests
{
    private static readonly Guid SeasonId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Match1 = Guid.Parse("55555555-5555-5555-5555-555555555501");
    private static readonly Guid Match2 = Guid.Parse("55555555-5555-5555-5555-555555555502");

    private static PredictionImportService PureService() => new(null!, null!, null!);

    private static List<RoundMatch> Matches() =>
    [
        new() { Id = Match1, HomeTeam = new Team { Name = "Arsenal" }, AwayTeam = new Team { Name = "Chelsea" } },
        new() { Id = Match2, HomeTeam = new Team { Name = "Liverpool" }, AwayTeam = new Team { Name = "Manchester City" } },
    ];

    private static List<User> Participants() =>
    [
        new() { Id = Guid.Parse("66666666-6666-6666-6666-666666666601"), Name = "João", Role = UserRole.Participant },
    ];

    [Fact]
    public void Parser_creates_candidates_for_known_participant_and_matches()
    {
        var text = "João\nArsenal 2x1 Chelsea\nLiverpool 1x1 Manchester City";

        var candidates = PureService().BuildCandidates(Guid.NewGuid(), Guid.NewGuid(), text, Matches(), Participants());

        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.NotNull(c.UserId));
        Assert.All(candidates, c => Assert.NotNull(c.RoundMatchId));
        Assert.All(candidates, c => Assert.False(c.NeedsReview));
        var first = candidates[0];
        Assert.Equal(2, first.PredictedHomeScore);
        Assert.Equal(1, first.PredictedAwayScore);
    }

    [Fact]
    public void Parser_resolves_common_abbreviations()
    {
        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nLiverpool 2x0 Man City", Matches(), Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match2, c.RoundMatchId);
    }

    [Theory]
    // OCR commonly misreads digits as letters in the score slot.
    [InlineData("Arsenal O x 1 Chelsea", 0, 1)]   // O -> 0
    [InlineData("Arsenal l x 2 Chelsea", 1, 2)]   // l -> 1
    [InlineData("Arsenal S x 0 Chelsea", 5, 0)]   // S -> 5
    [InlineData("Arsenal 3 x B Chelsea", 3, 8)]   // B -> 8
    public void Parser_canonicalises_ocr_digit_lookalikes(string line, int home, int away)
    {
        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\n{line}", Matches(), Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match1, c.RoundMatchId);
        Assert.Equal(home, c.PredictedHomeScore);
        Assert.Equal(away, c.PredictedAwayScore);
        Assert.False(c.NeedsReview);
    }

    [Fact]
    public void Parser_handles_whatsapp_screenshot_with_flags_and_header()
    {
        // Real format: title line, "Nome, Rodada N" header, flag emoji between
        // team and score, multi-word team names, trailing timestamp.
        var text =
            "Palpitão Copa do Mundo 2026\n" +
            "Gilberto, Rodada 2 (1a fase de grupos)\n" +
            "\n" +
            "Bélgica 🇧🇪 2 x 1 🇪🇬 Egito\n" +
            "Irã 🇮🇷 1 x 0 🇳🇿 Nova Zelândia\n" +
            "Espanha 🇪🇸 6 x 0 🇨🇻 Cabo Verde\n" +
            "Uruguai 🇺🇾 2 x 0 🇸🇦 Arábia Saudita\n" +
            "França 🇫🇷 2 x 1 🇸🇳 Senegal\n" +
            "Iraque 🇮🇶 0 x 3 🇳🇴 Noruega\n" +
            "Argentina 🇦🇷 3 x 0 🇩🇿 Argélia\n" +
            "Áustria 🇦🇹 2 x 0 🇯🇴 Jordânia\n" +
            "Portugal 🇵🇹 4 x 0 🇨🇩 RD Congo\n" +
            "Uzbequistão 🇺🇿 1 x 2 🇨🇴 Colômbia\n" +
            "Inglaterra 🏴 2 x 2 🇭🇷 Croácia\n" +
            "Gana 🇬🇭 2 x 0 🇵🇦 Panamá\n" +
            "12:21 ✓";

        var parsed = PureService().Parse(text);

        Assert.Equal(12, parsed.Count);
        // Participant comes from the "Gilberto, Rodada 2" header, not the title.
        Assert.All(parsed, p => Assert.Equal("Gilberto", p.ParticipantName));
        // Team names are clean (no emoji/flag surrogate noise left over).
        Assert.All(parsed, p => Assert.False((p.HomeTeamRaw + p.AwayTeamRaw).Any(char.IsSurrogate)));

        var belgica = parsed[0];
        Assert.Equal("Bélgica", belgica.HomeTeamRaw);
        Assert.Equal("Egito", belgica.AwayTeamRaw);
        Assert.Equal(2, belgica.HomeScore);
        Assert.Equal(1, belgica.AwayScore);

        var espanha = parsed[2];
        Assert.Equal("Espanha", espanha.HomeTeamRaw);
        Assert.Equal("Cabo Verde", espanha.AwayTeamRaw);
        Assert.Equal(6, espanha.HomeScore);

        var portugal = parsed[8];
        Assert.Equal("RD Congo", portugal.AwayTeamRaw);
    }

    [Fact]
    public void Parser_recovers_matches_from_noisy_real_ocr_output()
    {
        // Verbatim Tesseract output for a real WhatsApp screenshot: flag emoji are
        // read as stray glyphs ("R", "==", "=u", "mm"), some scores come out as
        // letters ("O" for 0), and three lines are too corrupted to read at all
        // (Uruguai/Argentina/Gana). The participant must stay "Gilberto" throughout.
        var text =
            "uetucdbdaino: Aucbod :\n" +
            "\n" +
            "Palpitão Copa do Mundo 2026 ;\n" +
            "Gilberto, Rodada 2 (1a fase de grupos) -\n" +
            "\n" +
            "Bélgica R 2x 1 == Egito\n" +
            "\n" +
            "Irã sS 1 x 07* Nova Zelândia\n" +
            "Espanha =u 6 x O == Cabo Verde\n" +
            "UT TEAA NEE RS TE\n" +
            "França N 2x1 H Senegal\n" +
            "\n" +
            "Iraque == O x 3 jlENoruega\n" +
            "PVrefenilara: ED ENJE\n" +
            "Áustria = 2 x O <==Jordânia\n" +
            "Portugal = 4x0PÉRD Congo\n" +
            "Uzbequistão &= 1 x 2 \"= Colômbia\n" +
            "Inglaterra mm 2 x 2 =&=Croácia\n" +
            "\n" +
            "(Fl E RD d PNS TEA N";

        var parsed = PureService().Parse(text);

        // 9 of 12 recovered; the 3 OCR-garbled lines are unrecoverable.
        Assert.Equal(9, parsed.Count);
        Assert.All(parsed, p => Assert.Equal("Gilberto", p.ParticipantName));

        Assert.Equal((2, 1), (parsed[0].HomeScore, parsed[0].AwayScore)); // Bélgica x Egito
        Assert.Equal("Bélgica", parsed[0].HomeTeamRaw);
        Assert.Equal("Egito", parsed[0].AwayTeamRaw);

        Assert.Equal((6, 0), (parsed[2].HomeScore, parsed[2].AwayScore)); // Espanha 6 x O(=0)
        Assert.Equal("Espanha", parsed[2].HomeTeamRaw);
        Assert.Equal("Cabo Verde", parsed[2].AwayTeamRaw);

        Assert.Equal((0, 3), (parsed[4].HomeScore, parsed[4].AwayScore)); // Iraque O(=0) x 3
    }

    [Theory]
    // Flag glyphs fused onto the away team (prefix junk).
    [InlineData("Time 1 x 0 ak=Noruega", "Noruega")]
    [InlineData("Time 1 x 0 s=Jordânia", "Jordânia")]
    [InlineData("Time 1 x 0 gRD Congo", "RD Congo")]
    [InlineData("Time 1 x 0 BlArgélia", "Argélia")]
    // Legitimate multi-word names must survive untouched.
    [InlineData("Time 1 x 0 Cabo Verde", "Cabo Verde")]
    [InlineData("Time 1 x 0 Arábia Saudita", "Arábia Saudita")]
    public void CleanTeam_peels_glued_flag_junk(string line, string expectedAway)
    {
        var parsed = Assert.Single(PureService().Parse($"Ana\n{line}"));
        Assert.Equal(expectedAway, parsed.AwayTeamRaw);
    }

    [Theory]
    // Flag glyphs left as a stray trailing token after the home team.
    [InlineData("Espanha eem 6 x 0 Cabo Verde", "Espanha", "Cabo Verde")]
    [InlineData("Uruguai EE 2 x 0 Arábia Saudita", "Uruguai", "Arábia Saudita")]
    [InlineData("Irã eh 1 x 0 Nova Zelândia", "Irã", "Nova Zelândia")]
    [InlineData("Argentina BE 3 x 0 Argélia", "Argentina", "Argélia")]
    public void CleanTeam_drops_trailing_flag_tokens(string line, string home, string away)
    {
        var parsed = Assert.Single(PureService().Parse($"Ana\n{line}"));
        Assert.Equal(home, parsed.HomeTeamRaw);
        Assert.Equal(away, parsed.AwayTeamRaw);
    }

    [Fact]
    public void Ambiguous_items_are_flagged_for_review()
    {
        // Unknown participant + a match that is not in the round.
        var text = "Desconhecido\nBarcelona 1x0 Madrid";

        var candidates = PureService().BuildCandidates(Guid.NewGuid(), Guid.NewGuid(), text, Matches(), Participants());

        var c = Assert.Single(candidates);
        Assert.Null(c.UserId);
        Assert.Null(c.RoundMatchId);
        Assert.True(c.NeedsReview);
    }

    // --- Confirm (DB-backed) ------------------------------------------------
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

    private static (Guid RoundId, Guid MatchId, Guid UserId) SeedRound(AppDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "João", Email = $"{userId}@x.com", PasswordHash = "x", Role = UserRole.Participant, IsActive = true, CreatedAt = DateTime.UtcNow });

        var round = new Round { Id = Guid.NewGuid(), SeasonId = SeasonId, Number = 1, Status = RoundStatus.Locked, CreatedByUserId = Admin, CreatedAt = DateTime.UtcNow };
        db.Rounds.Add(round);
        var match = new RoundMatch { Id = Guid.NewGuid(), RoundId = round.Id, Competition = Competition.PremierLeague, Phase = MatchPhase.Regular, HomeTeamId = SeedIds.Arsenal, AwayTeamId = SeedIds.Chelsea, StartsAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.RoundMatches.Add(match);
        db.SaveChanges();
        return (round.Id, match.Id, userId);
    }

    private static Guid SeedBatch(AppDbContext db, Guid roundId, Guid matchId, Guid? userId, int? home, int? away)
    {
        var batch = new OcrImportBatch { Id = Guid.NewGuid(), RoundId = roundId, UploadedByUserId = Admin, OriginalFileName = "x.png", LanguageUsed = "por", Status = OcrBatchStatus.Processed, CreatedAt = DateTime.UtcNow };
        db.OcrImportBatches.Add(batch);
        db.OcrPredictionCandidates.Add(new OcrPredictionCandidate
        {
            Id = Guid.NewGuid(),
            OcrImportBatchId = batch.Id,
            RoundId = roundId,
            UserId = userId,
            RoundMatchId = userId is null ? matchId : matchId,
            PredictedHomeScore = home,
            PredictedAwayScore = away,
            NeedsReview = userId is null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return batch.Id;
    }

    [Fact]
    public async Task Confirm_saves_predictions_as_adminocr()
    {
        using var db = CreateContext();
        var (roundId, matchId, userId) = SeedRound(db);
        var batchId = SeedBatch(db, roundId, matchId, userId, 2, 1);
        var service = new PredictionImportService(db, new AuditService(db), new FakeCurrentGroupService());

        await service.ConfirmAsync(batchId, Admin, Ct);

        var prediction = await db.Predictions.SingleAsync();
        Assert.Equal(PredictionSource.AdminOcr, prediction.Source);
        Assert.Equal(2, prediction.PredictedHomeScore);
        Assert.Equal(OcrBatchStatus.Confirmed, (await db.OcrImportBatches.FirstAsync()).Status);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "OcrImportConfirmed");
    }

    [Fact]
    public async Task Confirm_fails_with_incomplete_candidate()
    {
        using var db = CreateContext();
        var (roundId, matchId, _) = SeedRound(db);
        var batchId = SeedBatch(db, roundId, matchId, userId: null, home: 2, away: 1); // no participant
        var service = new PredictionImportService(db, new AuditService(db), new FakeCurrentGroupService());

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(batchId, Admin, Ct));
    }

    [Fact]
    public async Task Confirm_fails_when_already_confirmed()
    {
        using var db = CreateContext();
        var (roundId, matchId, userId) = SeedRound(db);
        var batchId = SeedBatch(db, roundId, matchId, userId, 2, 1);
        var service = new PredictionImportService(db, new AuditService(db), new FakeCurrentGroupService());
        await service.ConfirmAsync(batchId, Admin, Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(batchId, Admin, Ct));
        Assert.Equal("ocr.batchAlreadyConfirmed", ex.Key);
    }

    [Fact]
    public async Task Confirm_fails_with_duplicate_candidates()
    {
        using var db = CreateContext();
        var (roundId, matchId, userId) = SeedRound(db);
        var batchId = SeedBatch(db, roundId, matchId, userId, 2, 1);
        // A second candidate for the same participant+match (e.g. the same line OCRed twice).
        db.OcrPredictionCandidates.Add(new OcrPredictionCandidate
        {
            Id = Guid.NewGuid(),
            OcrImportBatchId = batchId,
            RoundId = roundId,
            UserId = userId,
            RoundMatchId = matchId,
            PredictedHomeScore = 0,
            PredictedAwayScore = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var service = new PredictionImportService(db, new AuditService(db), new FakeCurrentGroupService());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(batchId, Admin, Ct));
        Assert.Equal("ocr.duplicateCandidates", ex.Key);
        Assert.Empty(await db.Predictions.ToListAsync());
    }
}
