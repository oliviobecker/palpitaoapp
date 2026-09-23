using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Ocr;
using Xunit;

namespace Palpitao.Api.Tests.Ocr;

/// <summary>
/// What the screenshots of rounds 4–9 of the 2026/27 season taught the import, measured with the
/// real engine over 51 prints (see <see cref="OcrSamplesTests"/>): the participant read wrong on 19
/// of them, garbled clubs on the small WhatsApp Desktop prints, two rounds in one image, and scores
/// the kept reading got wrong while the others had them right. The OCR text below is verbatim
/// Tesseract output; the images themselves never enter the repository.
/// </summary>
public class OcrScreenshotRegressionTests
{
    private static readonly PredictionImportService Import = new(null!, null!, null!, null!);

    private static readonly Lazy<IReadOnlyCollection<string>> Catalogue = new(() =>
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db.Teams.AsNoTracking().Select(t => t.Name).ToList();
    });

    private static RoundMatch Fixture(string home, string away, int round = 9) => new()
    {
        Id = Guid.NewGuid(),
        Round = new Round { Number = round },
        HomeTeam = new Team { Name = home },
        AwayTeam = new Team { Name = away },
    };

    /// <summary>Round 9 as the catalogue spells it.</summary>
    private static List<RoundMatch> Round9() =>
    [
        Fixture("Brentford", "Chelsea"),
        Fixture("Tottenham", "Aston Villa"),
        Fixture("Brighton & Hove Albion", "Arsenal"),
        Fixture("Newcastle", "Hull City"),
        Fixture("Everton", "Ipswich Town"),
        Fixture("Nottingham Forest", "Coventry City"),
        Fixture("Bournemouth", "Liverpool"),
        Fixture("Leeds United", "Crystal Palace"),
        Fixture("Manchester City", "Sunderland"),
        Fixture("Fulham", "Manchester United"),
        Fixture("Luton Town", "Bradford City"),
    ];

    private static readonly User Becker = new() { Id = Guid.NewGuid(), Name = "Olivio Becker" };
    private static readonly User Valter = new() { Id = Guid.NewGuid(), Name = "Valter Silva" };
    private static readonly User Gilberto = new() { Id = Guid.NewGuid(), Name = "Gilberto Sales" };
    private static readonly User Felipe = new() { Id = Guid.NewGuid(), Name = "Felipe de Farias" };
    private static readonly User Ezau = new() { Id = Guid.NewGuid(), Name = "Ezaú Moura" };
    private static readonly User Vilaca = new() { Id = Guid.NewGuid(), Name = "Bruno Vilaça" };
    private static readonly User Pl = new() { Id = Guid.NewGuid(), Name = "PL" };

    private static List<User> Roster() => [Becker, Valter, Gilberto, Felipe, Ezau, Vilaca, Pl];

    // WhatsApp Desktop, 454x376: the Flávio rule sits on one line, and three clubs come back garbled.
    private const string BeckerRound9 =
        "Palpitao England 2026/2027\n" +
        "Becker, Rodada 9\n" +
        "\n" +
        "Palpites até 14h59 de sexta-feira (18/09/2026):\n" +
        "REGRA FLÁVIO: GBruno Vilaça tem sté 24 horas para palpitar\n" +
        "\n" +
        "Premier League\n" +
        "Brentford 1 x 2 Chelsea\n" +
        "Torrenham 2 x 1 Aston Villa\n" +
        "Brighton O x 2 Arsenal\n" +
        "Newcastle 3 x 1 Hll\n" +
        "Everton 2 x O Ipswich\n" +
        "Nottingham 2 x O Coventry\n" +
        "Boumemouth 1 x 2 Liverpool\n" +
        "Leeds 2 x 2 Crystal Palace\n" +
        "Man City 3 x 0 Sunderland\n" +
        "Fulham 1 x 2 Man Utd\n" +
        "\n" +
        "League One\n" +
        "Lmon 1 x 0 Bradford (x2)\n" +
        "\n" +
        "Regras: x2/x3 = multiplicador do jogo (clássicos, League One, mata-mate). 729 114 /";

    private static OcrImportResult Build(
        string text, string? fileName = null, IReadOnlyList<RoundMatch>? matches = null,
        IReadOnlyList<RoundMatch>? otherRounds = null) =>
        Import.BuildCandidates(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new OcrReading("original", text, 0.8f)],
            new OcrImportContext(matches ?? Round9(), Roster(), null, fileName, Catalogue.Value, otherRounds));

    // --- Participant --------------------------------------------------------

    [Fact]
    public void The_flavio_rule_line_does_not_take_the_participant_from_the_header()
    {
        var parsed = OcrTextParser.Parse(BeckerRound9);

        Assert.Equal(11, parsed.Count);
        Assert.All(parsed, p => Assert.Equal("Becker", p.ParticipantName));
        Assert.All(parsed, p => Assert.True(p.ParticipantAnnounced));
    }

    [Theory]
    [InlineData("JP Rodada 9", "JP")]
    [InlineData("PL Rodada 8", "PL")]
    [InlineData("Nome, Defarias, Rodada 9.", "Defarias")]
    [InlineData("Nome, Felipe Rodada 8", "Felipe")]
    [InlineData("*Nome*, Rodada 5", null)]
    [InlineData("PALPITAO ENGLAND — RODADA 9", null)]
    public void The_round_header_names_the_participant(string header, string? expected)
    {
        var parsed = Assert.Single(OcrTextParser.Parse($"{header}\nArsenal 2x1 Chelsea"));
        Assert.Equal(expected, parsed.ParticipantName);
    }

    [Theory]
    // Headings as OCR returned them, and the tail of the Flávio rule line once the bubble wraps it.
    [InlineData("Premier Leaque")]
    [InlineData("FPremies League")]
    [InlineData("Chempionship")]
    [InlineData("palpitar")]
    public void The_round_messages_own_words_are_not_a_participant(string line)
    {
        var parsed = Assert.Single(OcrTextParser.Parse($"{line}\nArsenal 2x1 Chelsea"));
        Assert.Null(parsed.ParticipantName);
    }

    [Fact]
    public void A_name_labelling_its_fixtures_still_names_them()
    {
        var parsed = OcrTextParser.Parse("Pedro: Arsenal 2x1 Chelsea, Liverpool 1x1 Manchester City");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, p => Assert.Equal("Pedro", p.ParticipantName));
    }

    [Theory]
    [InlineData("Valter.png", "Valter")]
    [InlineData("Valter1.jpeg", "Valter")]
    [InlineData("Valter (1).jpeg", "Valter")]
    [InlineData("ezau4e5.jpeg", "ezau")]
    [InlineData("DeFarias.jpeg", "De Farias")]
    [InlineData("EzauUnica.jpeg", "Ezau Unica")]
    [InlineData("Vilaç a.jpeg", "Vilaç a")]
    [InlineData("Vilaça.jpeg", "Vilaça")] // decomposed ç, as iOS sends it
    [InlineData("9.png", null)]
    [InlineData("IMG_2041.jpg", null)]
    [InlineData("WhatsApp.jpg", null)]
    [InlineData("WhatsApp Image 2026-09-18 at 14.42.10.jpeg", null)]
    [InlineData("Captura de Tela 2026-09-18 às 14.42.png", null)]
    [InlineData("photo_2026-09-18_14-42.jpg", null)]
    [InlineData("unnamed.png", null)]
    [InlineData("palpites.png", null)]
    public void The_file_name_is_read_as_a_participant_only_when_it_names_one(string file, string? expected)
    {
        Assert.Equal(expected, OcrTextParser.NameFromFileName(file));
    }

    [Theory]
    [InlineData("Becker", "Olivio Becker")]
    [InlineData("De Farias", "Felipe de Farias")]
    [InlineData("defarias", "Felipe de Farias")]
    [InlineData("Vilacao", "Bruno Vilaça")]
    [InlineData("Vilaç a", "Bruno Vilaça")]
    [InlineData("Ezau Unica", "Ezaú Moura")]
    [InlineData("PL", "PL")]
    [InlineData("Complete", null)] // contains "pl", but is nobody
    public void The_file_name_resolves_to_the_participant_it_names(string stem, string? expected)
    {
        var id = OcrTeamMatcher.ResolveParticipantFromFileName(stem, Roster());
        Assert.Equal(expected, Roster().SingleOrDefault(p => p.Id == id)?.Name);
    }

    [Fact]
    public void A_file_name_that_fits_two_people_resolves_to_nobody()
    {
        // Two members, "Bruno" and "Vilaça": the whole name fits both, and the first word alone
        // must not then pick one of them.
        List<User> roster = [new() { Id = Guid.NewGuid(), Name = "Bruno" }, new() { Id = Guid.NewGuid(), Name = "Vilaça" }];

        Assert.Null(OcrTeamMatcher.ResolveParticipantFromFileName("Bruno Vilaça", roster));
    }

    [Fact]
    public void The_file_name_files_every_row_under_its_participant()
    {
        var result = Build(BeckerRound9, fileName: "Becker.jpeg");

        Assert.Equal(11, result.Candidates.Count);
        Assert.All(result.Candidates, c => Assert.Equal(Becker.Id, c.UserId));
        // What OCR read stays what OCR read: the file name is not passed off as it.
        Assert.All(result.Candidates, c => Assert.Equal("Becker", c.ParticipantNameRaw));
    }

    [Fact]
    public void A_header_naming_someone_else_keeps_the_file_participant_but_goes_to_review()
    {
        var result = Build("Gilberto, Rodada 9\nBrentford 2x1 Chelsea", fileName: "Valter.png");

        var c = Assert.Single(result.Candidates);
        Assert.Equal(Valter.Id, c.UserId);
        Assert.True(c.NeedsReview);
        Assert.Contains("Gilberto", c.ReviewNotes);
    }

    [Fact]
    public void A_file_name_that_names_nobody_leaves_the_header_to_decide()
    {
        var result = Build("Nome, Defarias, Rodada 9\nBrentford 1 x O Chelsea", fileName: "9.png");

        Assert.Equal(Felipe.Id, Assert.Single(result.Candidates).UserId);
    }

    // --- Fixtures -----------------------------------------------------------

    [Fact]
    public void Garbled_clubs_on_a_small_screenshot_resolve_through_the_clean_side_and_go_to_review()
    {
        var round = Round9();
        var result = Build(BeckerRound9, fileName: "Becker.jpeg", matches: round);

        Assert.All(result.Candidates, c => Assert.NotNull(c.RoundMatchId));
        Assert.Equal(11, result.Candidates.Select(c => c.RoundMatchId).Distinct().Count());

        var approximate = result.Candidates.Where(c => c.NeedsReview).Select(c => c.MatchTextRaw).ToList();
        Assert.Equal(
            ["Torrenham 2 x 1 Aston Villa", "Newcastle 3 x 1 Hll", "Lmon 1 x 0 Bradford (x2)"],
            approximate);
        Assert.All(result.Candidates.Where(c => c.NeedsReview), c => Assert.Contains("aproximação", c.ReviewNotes));
    }

    [Theory]
    [InlineData("Touenham", "Aston Villia", "Tottenham")]
    [InlineData("EBrigihton", "Arsensl", "Brighton & Hove Albion")]
    [InlineData("Man Cêty", "Sundestand", "Manchester City")]
    public void Both_sides_misread_still_resolve_when_one_is_within_a_letter(string home, string away, string club)
    {
        var round = Round9();
        var resolution = OcrTeamMatcher.Resolve(home, away, round, Catalogue.Value);

        Assert.True(resolution.Approximate);
        Assert.Equal(round.Single(m => m.HomeTeam!.Name == club).Id, resolution.MatchId);
    }

    [Fact]
    public void Another_club_is_not_taken_for_a_misreading()
    {
        // Another round's line on this card: Brentford pins Brentford x Tottenham, and "Nottingham"
        // looks enough like Tottenham to pass as a misreading — but it is a club of its own.
        List<RoundMatch> round = [Fixture("Brentford", "Tottenham"), Fixture("Leeds United", "Crystal Palace")];

        Assert.Null(OcrTeamMatcher.ResolveMatch("Brentford", "Nottingham", round, Catalogue.Value));
    }

    [Fact]
    public void Without_the_catalogue_the_approximate_tier_stays_off()
    {
        Assert.Null(OcrTeamMatcher.ResolveMatch("Torrenham", "Aston Villa", Round9()));
    }

    [Theory]
    // OCR added the m rather than turning rn into one; the ligature folding made these worse.
    [InlineData("Bournemmouth", "Brentford", "Bournemouth", "Brentford")]
    [InlineData("Preston", "Blackburmn", "Preston North End", "Blackburn Rovers")]
    public void A_doubled_m_is_one_letter_off(string home, string away, string clubHome, string clubAway)
    {
        List<RoundMatch> round = [Fixture(clubHome, clubAway), Fixture("Leeds United", "Crystal Palace")];

        var resolution = OcrTeamMatcher.Resolve(home, away, round, Catalogue.Value);

        Assert.Equal(round[0].Id, resolution.MatchId);
        Assert.False(resolution.Approximate);
    }

    [Theory]
    [InlineData("Luton 1 x O Bradford (:2)", 1, 0)] // "(×2)" read as "(:2)" used to name a participant
    [InlineData("Bolton OxO0 Cardiff", 0, 0)]
    [InlineData("Tottenham O0x0O Aston Villa", 0, 0)]
    [InlineData("Lincoln O0x0 Swansea", 0, 0)]
    public void Lines_that_used_to_be_lost_become_fixtures(string line, int home, int away)
    {
        var parsed = Assert.Single(OcrTextParser.Parse(line));
        Assert.Equal((home, away), (parsed.HomeScore, parsed.AwayScore));
    }

    [Theory]
    [InlineData("— Gilberto Sales +55 85 98934-0476\nBotei o número 8 errado 14:46")]
    [InlineData("~Valter Silva +55 61 98167-1289\nEstou na série C, e você?")]
    [InlineData("mata). 22:897 - ee .)")]
    [InlineData("UTITIITTTATOY 22:238/ Palpitao England 2026/2027")]
    public void Chat_numbers_are_not_scores(string text)
    {
        Assert.Empty(OcrTextParser.Parse(text));
    }

    // --- Readings -----------------------------------------------------------

    private static OcrImportResult BuildReadings(params OcrReading[] readings) =>
        Import.BuildCandidates(
            Guid.NewGuid(),
            Guid.NewGuid(),
            readings,
            new OcrImportContext(
                [Fixture("Lincoln City", "Southampton"), Fixture("Preston North End", "Blackburn Rovers")],
                Roster(),
                Language: "pt"));

    [Fact]
    public void The_reading_that_resolves_more_fixtures_wins_over_the_more_confident_one()
    {
        var result = BuildReadings(
            new OcrReading("original", "PL Rodada 4\nLincoln 1x1 Southampton", 0.9f),
            new OcrReading("prepared", "PL Rodada 4\nLincoln 1x1 Southampton\nPreston 0x2 Blackburn", 0.6f));

        Assert.Equal("prepared", result.Reading.Variant);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void A_score_the_readings_disagree_on_goes_to_review_even_when_most_of_them_agree()
    {
        // 4/neto: the image says 1x1; two of the three readings said 0x1. A vote would have kept the
        // wrong one — the disagreement itself is the signal.
        var result = BuildReadings(
            new OcrReading("original", "PL Rodada 4\nLincoln 1x1 Southampton", 0.8f),
            new OcrReading("prepared", "PL Rodada 4\nLincoln 0x1 Southampton", 0.9f),
            new OcrReading("prepared+inverted", "PL Rodada 4\nLincoln 0x1 Southampton", 0.7f));

        var c = Assert.Single(result.Candidates);
        Assert.True(c.NeedsReview);
        Assert.Contains("0x1 / 1x1", c.ReviewNotes);
    }

    [Fact]
    public void A_score_read_off_a_letter_goes_to_review_unless_another_reading_saw_digits()
    {
        var alone = BuildReadings(new OcrReading("original", "PL Rodada 4\nLincoln Sx1 Southampton", 0.9f));
        var confirmed = BuildReadings(
            new OcrReading("original", "PL Rodada 4\nLincoln Sx1 Southampton", 0.9f),
            new OcrReading("prepared", "PL Rodada 4\nLincoln 5x1 Southampton", 0.8f));

        Assert.True(Assert.Single(alone.Candidates).NeedsReview);
        Assert.False(Assert.Single(confirmed.Candidates).NeedsReview);
    }

    // --- Another round ------------------------------------------------------

    [Fact]
    public void Another_rounds_fixtures_in_the_same_screenshot_are_left_out_and_counted()
    {
        // "Ezaú, Rodada 6 e 7": one header, both rounds' lists under it.
        var result = Build(
            "Ezaú, Rodada 6 e 7\n" +
            "Championship\n" +
            "West Ham 2 x 0 Wrexham\n" +
            "Derby 1 x 1 Birmingham\n" +
            "Premier League\n" +
            "Bournemouth 1 x 1 Brentford\n" +
            "Man Utd 1 x 2 Man City (x2)",
            fileName: "Ezau.jpeg",
            matches: [Fixture("West Ham United", "Wrexham", 6), Fixture("Derby County", "Birmingham City", 6)],
            otherRounds: [Fixture("Bournemouth", "Brentford", 7), Fixture("Manchester United", "Manchester City", 7)]);

        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, c => Assert.Equal(Ezau.Id, c.UserId));
        Assert.Equal(2, result.IgnoredLineCount);
        Assert.Equal(["7"], result.IgnoredRoundLabels);
    }

    [Fact]
    public void A_line_that_fits_no_round_stays_for_review()
    {
        // Real clubs, but no fixture of any round: an outdated message, a typo. Dropping it would
        // lose a prediction without anyone seeing it.
        var result = Build(
            "Ezaú, Rodada 6\nWest Ham 2 x 0 Wrexham\nFulham 1 x 0 Everton",
            matches: [Fixture("West Ham United", "Wrexham", 6)],
            otherRounds: [Fixture("Bournemouth", "Brentford", 7)]);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Contains(result.Candidates, c => c.RoundMatchId is null && c.NeedsReview);
        Assert.Equal(0, result.IgnoredLineCount);
    }
}
