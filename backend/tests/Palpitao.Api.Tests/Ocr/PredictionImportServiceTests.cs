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

    /// <summary>Parsing/matching only — nothing it touches reaches the database.</summary>
    private static PredictionImportService PureService() => new(null!, null!, null!, null!);

    /// <summary>The real thing, wired to a context: needed by ConfirmAsync and alias learning.</summary>
    private static PredictionImportService ImportService(AppDbContext db)
    {
        var current = new FakeCurrentGroupService();
        var audit = new AuditService(db);
        return new PredictionImportService(db, audit, current, new OcrAliasService(db, audit, current));
    }

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
    // One letter dropped or misread by OCR — the real failure from a dark-mode WhatsApp
    // screenshot, where "Coventry" came back as "Coventy" and cost the whole row.
    [InlineData("Coventy")]
    [InlineData("Coventrv")]
    [InlineData("Goventry")]
    public void Parser_tolerates_a_single_letter_the_ocr_got_wrong(string mangled)
    {
        List<RoundMatch> matches =
        [
            new() { Id = Match1, HomeTeam = new Team { Name = "Arsenal" }, AwayTeam = new Team { Name = "Coventry City" } },
        ];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\nArsenal 3 x 1 {mangled}", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match1, c.RoundMatchId);
        Assert.False(c.NeedsReview);
        Assert.Equal(1.0, c.Confidence);
    }

    [Theory]
    // "rn" and "m" are the same few pixels at screenshot resolution, and this group's clubs are
    // full of them. Every one of these is two edits from its club — one past the budget — and the
    // budget cannot be widened, because Barnsley and Burnley are two edits apart too. Seven rows
    // across ten real screenshots died here before the folding.
    [InlineData("Blackbum", "Blackburn Rovers")]
    [InlineData("Bumley", "Burnley")]
    [InlineData("Boumesmouth", "Bournemouth")]
    public void Parser_reads_the_rn_that_ocr_returned_as_an_m(string mangled, string club)
    {
        List<RoundMatch> matches =
        [
            new() { Id = Match1, HomeTeam = new Team { Name = "Arsenal" }, AwayTeam = new Team { Name = club } },
        ];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\nArsenal 3 x 1 {mangled}", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match1, c.RoundMatchId);
        Assert.False(c.NeedsReview);
    }

    [Fact]
    public void The_rn_folding_still_keeps_barnsley_and_burnley_apart()
    {
        // The pair the edit budget exists to protect. Neither carries an m, so the folding never
        // touches them — but the guard belongs next to the change that could have broken it.
        List<RoundMatch> matches =
        [
            new() { Id = Match1, HomeTeam = new Team { Name = "Arsenal" }, AwayTeam = new Team { Name = "Barnsley" } },
            new() { Id = Match2, HomeTeam = new Team { Name = "Chelsea" }, AwayTeam = new Team { Name = "Burnley" } },
        ];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nArsenal 3 x 1 Burnley", matches, Participants());

        // "Arsenal x Burnley" is not on the card at all, and no near-miss may invent it.
        Assert.Null(Assert.Single(candidates).RoundMatchId);
    }

    [Fact]
    public void Parser_reads_a_score_whose_separator_ocr_returned_twice()
    {
        // "Millwall 2xX1 Norwich" — one glyph read as two, on two of the ten screenshots.
        var parsed = Assert.Single(PureService().Parse("João\nMillwall 2xX1 Norwich"));

        Assert.Equal("Millwall", parsed.HomeTeamRaw);
        Assert.Equal("Norwich", parsed.AwayTeamRaw);
        Assert.Equal((2, 1), (parsed.HomeScore, parsed.AwayScore));
    }

    [Theory]
    // The round keyword gets one wrong character too. "Feunppe, Kodada 1" is a real header: a
    // capital R read as a K used to throw the participant away entirely, leaving the admin with
    // no name to correct and nothing for the group to learn.
    // One character *substituted*, which is what OCR does — first, middle or last. An inserted or
    // dropped letter is not covered on purpose: "Rodadas" is a different word, not a misread one.
    [InlineData("Feunppe, Kodada 1", "Feunppe")]
    [InlineData("Gilberto, Rodadn 2", "Gilberto")]
    [InlineData("Ana - Roduda 3", "Ana")]
    public void Parser_reads_a_round_header_whose_keyword_ocr_mangled(string header, string expected)
    {
        var parsed = Assert.Single(PureService().Parse($"{header}\nArsenal 2x1 Chelsea"));

        Assert.Equal(expected, parsed.ParticipantName);
    }

    [Theory]
    [InlineData("Premier League")]
    [InlineData("Championship")]
    [InlineData("League One")]
    [InlineData("FA Cup")]
    public void Parser_does_not_mistake_a_competition_heading_for_the_participant(string heading)
    {
        // The message prints the participant header first and a competition heading above each
        // block. Both are bare letters-and-spaces lines, so the heading used to overwrite the
        // participant and every fixture below it was filed against nobody.
        var text = $"Becker, Rodada 2\n{heading}\nArsenal 2x1 Chelsea";
        List<User> participants = [new() { Id = Guid.NewGuid(), Name = "Becker", Role = UserRole.Participant }];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), text, Matches(), participants);

        var c = Assert.Single(candidates);
        Assert.Equal("Becker", c.ParticipantNameRaw);
        Assert.Equal(participants[0].Id, c.UserId);
        Assert.False(c.NeedsReview);
    }

    [Fact]
    public void Parser_tolerates_an_accent_the_ocr_dropped_from_a_participant()
    {
        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "Joao\nArsenal 2x1 Chelsea", Matches(), Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Participants()[0].Id, c.UserId);
        Assert.False(c.NeedsReview);
    }

    [Theory]
    // Too short to risk it: one edit already turns these into a different real club.
    [InlineData("Hul")]
    [InlineData("Stok")]
    public void Parser_does_not_guess_at_a_short_mangled_name(string mangled)
    {
        List<RoundMatch> matches =
        [
            new() { Id = Match1, HomeTeam = new Team { Name = "Hull City" }, AwayTeam = new Team { Name = "Stoke City" } },
        ];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\n{mangled} 3 x 1 Wrexham", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Null(c.RoundMatchId);
        Assert.True(c.NeedsReview);
    }

    [Fact]
    public void Parser_refuses_when_two_fixtures_are_equally_close_to_the_mangled_name()
    {
        // "Barnslex" is one edit from Barnsley and Barnsler alike: ambiguity must stay
        // ambiguous rather than pick the first fixture on the card.
        List<RoundMatch> matches =
        [
            new() { Id = Match1, HomeTeam = new Team { Name = "Barnsley" }, AwayTeam = new Team { Name = "Wrexham" } },
            new() { Id = Match2, HomeTeam = new Team { Name = "Barnsler" }, AwayTeam = new Team { Name = "Wrexham" } },
        ];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nBarnslex 3 x 1 Wrexham", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Null(c.RoundMatchId);
        Assert.True(c.NeedsReview);
    }

    [Fact]
    public void Parser_keeps_an_ambiguous_strict_match_ambiguous()
    {
        // Both Sheffields on the card: the strict pass finds two, so the fuzzy tier must not
        // run at all — it could otherwise break the tie on an edit distance nobody asked for.
        List<RoundMatch> matches =
        [
            new() { Id = Match1, HomeTeam = new Team { Name = "Sheffield United" }, AwayTeam = new Team { Name = "Wrexham" } },
            new() { Id = Match2, HomeTeam = new Team { Name = "Sheffield Wednesday" }, AwayTeam = new Team { Name = "Wrexham" } },
        ];

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nSheffield 3 x 1 Wrexham", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Null(c.RoundMatchId);
        Assert.True(c.NeedsReview);
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

    [Theory]
    // A line returned exactly as it was copied, with no score filled in. The message
    // template's own " x " separator looks like a score pair once OCR letter
    // look-alikes are allowed on both sides ("Arsenal x Chelsea" → "Arsena" 1 x 1
    // "helsea"), and both halves still resolve — so without a real digit the import
    // would confidently store a prediction nobody made.
    [InlineData("Arsenal x Chelsea")]
    [InlineData("Liverpool x Manchester City")]
    // The dangerous one: "l x L" are both letter look-alikes, so this is the line the
    // isolated-pair allowance below must keep rejecting.
    [InlineData("Arsenal x Leeds")]
    public void Parser_ignores_a_line_left_without_a_score(string line)
    {
        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\n{line}", Matches(), Participants());

        Assert.Empty(candidates);
    }

    [Fact]
    public void Parser_resolves_the_short_names_printed_in_the_group_message()
    {
        // "Man City" is an alias; "Liverpool" needs no help. Both come straight from
        // the copy-ready message (frontend shared/utils/team-name.util.ts).
        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nLiverpool 2 x 0 Man City", Matches(), Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match2, c.RoundMatchId);
        Assert.False(c.NeedsReview);
    }

    [Fact]
    public void Short_name_still_matches_a_team_the_fixture_feed_named_that_way()
    {
        // Providers ship "Man City"/"Wolves" as the team name and FixtureImportService
        // creates the row verbatim, so aliasing the raw name must not stop it matching
        // a Team that already carries the short form.
        var matches = new List<RoundMatch>
        {
            new() { Id = Match1, HomeTeam = new Team { Name = "Wolves" }, AwayTeam = new Team { Name = "Man City" } },
        };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nWolves 1 x 2 Man City", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match1, c.RoundMatchId);
        Assert.False(c.NeedsReview);
    }

    [Fact]
    public void Feed_named_liverpool_still_matches_the_short_name_the_message_prints()
    {
        // The feed spells it "Liverpool FC" and FixtureImportService stores that verbatim,
        // so a round can carry either spelling. The message prints "Liverpool" for both
        // (frontend team-name.util.ts), and that has to come back to this match.
        var matches = new List<RoundMatch>
        {
            new() { Id = Match1, HomeTeam = new Team { Name = "Liverpool FC" }, AwayTeam = new Team { Name = "Chelsea" } },
        };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nLiverpool 2 x 1 Chelsea", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(Match1, c.RoundMatchId);
        Assert.False(c.NeedsReview);
    }

    [Fact]
    public void Ampersand_club_matches_even_though_ocr_drops_the_symbol()
    {
        // CleanTeam keeps letters only, so the seeded "Brighton & Hove Albion" is read
        // back as "Brighton Hove Albion".
        var brightonMatch = Guid.NewGuid();
        var matches = new List<RoundMatch>
        {
            new() { Id = brightonMatch, HomeTeam = new Team { Name = "Brighton & Hove Albion" }, AwayTeam = new Team { Name = "Arsenal" } },
        };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "João\nBrighton & Hove Albion 1 x 0 Arsenal", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(brightonMatch, c.RoundMatchId);
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

    /// <summary>
    /// The 12 Championship fixtures of the round the group actually imported, spelled the way
    /// the fixture feed stores them — so the short names the screenshots carry ("Wolves",
    /// "West Brom", "Sheffield Utd", "QPR") have to travel the alias path to get back here.
    /// </summary>
    private static List<RoundMatch> ChampionshipRound() =>
    [
        Fixture("Wolverhampton Wanderers", "Blackburn Rovers"),
        Fixture("Bolton Wanderers", "Preston North End"),
        Fixture("Charlton Athletic", "Derby County"),
        Fixture("Middlesbrough", "Lincoln City"),
        Fixture("Norwich City", "West Bromwich Albion"),
        Fixture("Portsmouth", "Queens Park Rangers"),
        Fixture("Stoke City", "Swansea City"),
        Fixture("Bristol City", "Millwall"),
        Fixture("Sheffield United", "Birmingham City"),
        Fixture("Watford", "Southampton"),
        Fixture("Burnley", "West Ham United"),
        Fixture("Cardiff City", "Wrexham"),
    ];

    private static RoundMatch Fixture(string home, string away) => new()
    {
        Id = Guid.NewGuid(),
        HomeTeam = new Team { Name = home },
        AwayTeam = new Team { Name = away },
    };

    [Fact]
    public void Parser_reads_the_whatsapp_screenshot_headed_by_the_participant_and_the_round()
    {
        // Verbatim Tesseract output of an image the group imported (batch a57e21ca). Everything
        // here used to go wrong at once: the name sat behind a WhatsApp bold marker and had no
        // comma before "Rodada", "Norwich O x O" lost both zeros to the letter O, the last line
        // carried a clock, and "Weolves" was one letter off.
        var text =
            "Palpitao England 2026/2027\n" +
            "*Flavio Rodada 1 (RODADA TESTE\n" +
            "NAÃO PONTUA PRO PALPITAO)\n" +
            "\n" +
            "Palpites até 14h59 de sexta-feira\n" +
            "(14/08/2026):\n" +
            "\n" +
            "Championship\n" +
            "\n" +
            "Weolves 3 x 2 Blackburn\n" +
            "\n" +
            "Bolton O x 2 Preston\n" +
            "\n" +
            "Charlton 4 x 1 Derby\n" +
            "Middlesbrough 2 x 2 Lincoln\n" +
            "Norwich O x O West Brom\n" +
            "Portsmouth 2 x 1 QPR\n" +
            "\n" +
            "Stoke 1 x O Swansea\n" +
            "\n" +
            "Bristol City O x 1 Millwall\n" +
            "Sheffield Utd 1 x 2 Birmingham\n" +
            "Watford 2 x O Southampton\n" +
            "Burnley O x 2 West Ham\n" +
            "Cardiff 1x 3 Wrexham 01:06 Z\n";

        List<User> participants = [new() { Id = Guid.NewGuid(), Name = "Flavio", Role = UserRole.Participant }];
        var candidates = PureService()
            .BuildCandidates(Guid.NewGuid(), Guid.NewGuid(), text, ChampionshipRound(), participants);

        Assert.Equal(12, candidates.Count);
        Assert.All(candidates, c => Assert.Equal("Flavio", c.ParticipantNameRaw));
        Assert.All(candidates, c => Assert.Equal(participants[0].Id, c.UserId));
        Assert.All(candidates, c => Assert.NotNull(c.RoundMatchId));
        Assert.All(candidates, c => Assert.False(c.NeedsReview));

        var norwich = Assert.Single(candidates, c => c.MatchTextRaw!.StartsWith("Norwich", StringComparison.Ordinal));
        Assert.Equal(0, norwich.PredictedHomeScore);
        Assert.Equal(0, norwich.PredictedAwayScore);

        var cardiff = Assert.Single(candidates, c => c.MatchTextRaw!.StartsWith("Cardiff", StringComparison.Ordinal));
        Assert.Equal(1, cardiff.PredictedHomeScore);
        Assert.Equal(3, cardiff.PredictedAwayScore);
    }

    [Fact]
    public void Parser_reads_the_screenshot_whose_name_only_follows_a_palpites_prefix()
    {
        // Batch 78a9fcb6: the chat header above the scores is pure OCR noise, and the only
        // usable name is the ALL-CAPS "PALPITES PL" the group writes. "nAc" (junk) and
        // "Paraguaio" (a nickname) both look like names and both used to win.
        var text =
            "12:32 B * SE\n" +
            "ÃL )\n" +
            "ALAFCA\n" +
            "nAc\n" +
            "Palpitão England 26/27 v\n" +
            "Bruno, De Farias, Dr. Flavio Cr...\n" +
            "X4 REGRAS:\n" +
            "U-ULZ\n" +
            "\"Paraguaio\"\n" +
            "Palpitão England 2026/2027\n" +
            "— Rodada 1\n" +
            "PALPITES PL\n" +
            "Wolves 2x0 Blackburn\n" +
            "Bolton 2x1 Preston\n" +
            "Charlton 1x0 Derby\n" +
            "Middlesbrough 3x1 Lincoln\n" +
            "Norwich 1x1 West Brom\n" +
            "Portsmouth 1x2 QPR\n" +
            "Stoke 1x2 Swansea\n" +
            "Bristol City 2x0 Millwall\n" +
            "Sheffield Utd 2x1 Birmingham\n" +
            "Watford Ox2 Southampton\n" +
            "Burnley 2x1 West Ham\n" +
            "Cardiff 1x2 Wrexham 17:35 ,\n";

        List<User> participants = [new() { Id = Guid.NewGuid(), Name = "PL", Role = UserRole.Participant }];
        var candidates = PureService()
            .BuildCandidates(Guid.NewGuid(), Guid.NewGuid(), text, ChampionshipRound(), participants);

        Assert.Equal(12, candidates.Count);
        Assert.All(candidates, c => Assert.Equal("PL", c.ParticipantNameRaw));
        Assert.All(candidates, c => Assert.False(c.NeedsReview));
    }

    [Fact]
    public void Parser_reads_the_screenshot_whose_header_carries_the_name_and_the_round_together()
    {
        // Verbatim engine output for the round the group actually posted, once the untouched
        // image was allowed to compete with the binarized ones. Two things used to go wrong at
        // once: the only usable name is behind "PALPITES" with the round glued to it, so the
        // parser fell back on the sender's contact name at the top of the bubble ("Pedro
        // Rodrigues", nobody on the roster); and the bubble broke two fixtures onto a second line
        // ("Birmingham 2 x 0" / "Bristol City", "QPR2x0" / "Bolton"), which cost the fixture and
        // handed the participant to a club for every row below it.
        var text =
            "Pedro Rodrigues\n" +
            "Palpitao England 2026/2027\n" +
            "PALPITES PL, Rodada 1\n" +
            "\n" +
            "Palpites até 14h59 de sexta-feira (21/08/2026):\n" +
            "\n" +
            "Premier League\n" +
            "\n" +
            "“Arsenal 4x0 Coventry\n" +
            "\n" +
            "Hull 1x0 Man Utd\n" +
            "\n" +
            "Everton 1 x 0 Crystal Palace\n" +
            "Ipswich 1 x 0 Sundertand\n" +
            "Nottingham 2x0 Leeds\n" +
            "Brentford 0 x 2 Tottenham\n" +
            "Brighton 1 x O Aston Villa\n" +
            "Man City 5x1 Bournemouth\n" +
            "Newocastle 2 x 4 Liverpool\n" +
            "Fulham 0 x 2 Chelsea\n" +
            "\n" +
            "Championship\n" +
            "Lincoln 2x1 Portsmouth\n" +
            "Millwall 1 x O Norwich\n" +
            "Birmingham 2 x 0\n" +
            "\n" +
            "Bristol City\n" +
            "Blackbum 0 x 2 Middlesbrough\n" +
            "Derby 1 x 0 Cardiff.\n" +
            "Preston 2 x 1 Wolves\n" +
            "QPR2x0\n" +
            "\n" +
            "Bolton\n" +
            "Southampton 2 x 1 Stoke\n" +
            "Swansea 1 x O Sheffield Utd\n" +
            "West Ham 2 x 1 Chariton\n" +
            "Wrexham 2 x 2 Watford\n" +
            "\n" +
            "West Brom 2 x O Bumley\n" +
            "\n" +
            "League One\n" +
            "Leicester 2 x O Burton (x2) 851Am\n" +
            "\n" +
            "Como vou tirar print dessa merda? ; -. ,,,";

        List<User> participants = [new() { Id = Guid.NewGuid(), Name = "PL", Role = UserRole.Participant }];
        var candidates = PureService()
            .BuildCandidates(Guid.NewGuid(), Guid.NewGuid(), text, PalpitaoEnglandRound(), participants);

        Assert.Equal(23, candidates.Count);
        Assert.All(candidates, c => Assert.Equal("PL", c.ParticipantNameRaw));

        // The two the bubble broke in half, back in one piece and resolved.
        var birmingham = Assert.Single(
            candidates, c => c.MatchTextRaw!.StartsWith("Birmingham", StringComparison.Ordinal));
        Assert.Equal((2, 0), (birmingham.PredictedHomeScore, birmingham.PredictedAwayScore));
        Assert.False(birmingham.NeedsReview);

        var qpr = Assert.Single(candidates, c => c.MatchTextRaw!.StartsWith("QPR", StringComparison.Ordinal));
        Assert.Equal((2, 0), (qpr.PredictedHomeScore, qpr.PredictedAwayScore));
        Assert.False(qpr.NeedsReview);

        // "Blackbum" and "Bumley" resolve through the rn/m folding, which is the confusion
        // Tesseract makes on every one of this group's screenshots. What is left is the bubble's
        // clock with its colon eaten: CleanTeam keeps "Am" because it is Title-case, so the away
        // side reads "Burton x Am" and no fixture fits. Marked for review, never guessed at.
        Assert.Equal(
            ["Leicester 2 x O Burton (x2) 851Am"],
            candidates.Where(c => c.NeedsReview).Select(c => c.MatchTextRaw));
    }

    [Fact]
    public void Parser_rejoins_a_fixture_the_bubble_wrapped_onto_a_second_line()
    {
        var parsed = Assert.Single(PureService().Parse("Birmingham 2 x 0\nBristol City"));

        Assert.Equal("Birmingham", parsed.HomeTeamRaw);
        Assert.Equal("Bristol City", parsed.AwayTeamRaw);
        Assert.Equal((2, 0), (parsed.HomeScore, parsed.AwayScore));
    }

    [Fact]
    public void A_wrapped_away_team_does_not_become_the_participant()
    {
        // The orphan half is name-shaped, so it used to be read as a person - taking every
        // fixture below it away from the participant the screenshot is actually about.
        var parsed = PureService().Parse("João\nBirmingham 2 x 0\nBristol City\nDerby 1x0 Cardiff");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, p => Assert.Equal("João", p.ParticipantName));
    }

    [Theory]
    // The line under a dangling score is glued to it only when it is really the other half of the
    // fixture. A name header or a competition heading announces the next block instead, and
    // swallowing one would cost the participant every row below it.
    [InlineData("PALPITES PL")]
    [InlineData("Championship")]
    public void Parser_does_not_glue_a_header_onto_a_dangling_score(string header)
    {
        var parsed = Assert.Single(
            PureService().Parse($"Birmingham 2 x 0\n{header}\nArsenal 2x1 Chelsea"));

        Assert.Equal("Arsenal", parsed.HomeTeamRaw);
    }

    [Fact]
    public void Parser_replays_the_ocr_output_the_group_actually_got_back()
    {
        // Verbatim "Texto extraido" of the batch, dark-mode screenshot, language "por". Tesseract
        // dropped ~20 of the 23 fixtures outright -- that is an engine problem, not a parsing one,
        // and this fixture exists to pin what the parser does with the little that survives.
        // The headings came back misspelled ("Championshio", "Leaque One"), which is exactly the
        // shape of a person's name, and they used to take the participant away from PL.
        var text =
            "Ó PesoRotigues\n" +
            "Palpitao England 2026/2027\n" +
            "PALPITES PL, Rodada 1\n" +
            "\n" +
            "Palpites até 14h59 de serta feira (21/08/2026):\n" +
            "\n" +
            "Brentford O x 2 Tottenham\n" +
            "\n" +
            "Championshio\n" +
            "Lincoin 2x1 Portemouih\n" +
            "\n" +
            "Leaque One\n" +
            "Leicester 2 x O Burion (X2) E51 AM\n" +
            "\n" +
            "Como vou tirar print dessa merda? » -. ..)\n" +
            "\n" +
            "-n";

        var pl = new User { Id = Guid.NewGuid(), Name = "PL", Role = UserRole.Participant };
        var candidates = PureService()
            .BuildCandidates(Guid.NewGuid(), Guid.NewGuid(), text, PalpitaoEnglandRound(), [pl]);

        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, c => Assert.Equal("PL", c.ParticipantNameRaw));
        Assert.All(candidates, c => Assert.Equal(pl.Id, c.UserId));

        // Brentford and Leicester resolve ("Burion" is one edit from Burton Albion); "Portemouih"
        // is two edits from Portsmouth, past the budget, so that one stays for the admin.
        var review = Assert.Single(candidates, c => c.NeedsReview);
        Assert.StartsWith("Lincoin", review.MatchTextRaw!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_misread_heading_does_not_take_the_participant_from_an_announced_name()
    {
        var parsed = Assert.Single(
            PureService().Parse("PALPITES PL\nChampionshio\nArsenal 2x1 Chelsea"));

        Assert.Equal("PL", parsed.ParticipantName);
    }

    [Fact]
    public void An_announced_name_still_wins_over_a_bare_one_read_before_it()
    {
        var parsed = Assert.Single(
            PureService().Parse("Ó PesoRotigues\nPALPITES PL\nArsenal 2x1 Chelsea"));

        Assert.Equal("PL", parsed.ParticipantName);
    }

    [Fact]
    public void A_dangling_score_does_not_swallow_the_fixture_under_it()
    {
        // "Arsenal 4x0" lost its away team to OCR. Gluing the next line onto it reads as a
        // fixture ("Arsenal 4 x 0 Hull x Man Utd"), so the count stays the same while a real
        // fixture is replaced by an invented one -- which is why this asserts the teams.
        var parsed = PureService()
            .Parse("João\nArsenal 4x0\nHull 1x0 Man Utd\nEverton 1 x 0 Crystal Palace");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, p => Assert.Equal("João", p.ParticipantName));

        Assert.Equal("Hull", parsed[0].HomeTeamRaw);
        Assert.Equal("Man Utd", parsed[0].AwayTeamRaw);
        Assert.Equal("Everton", parsed[1].HomeTeamRaw);
    }

    /// <summary>
    /// The 23 fixtures of that round, spelled the way the fixture feed stores them - so every
    /// short name the screenshot prints ("Man Utd", "Nottingham", "QPR", "Sheffield Utd",
    /// "West Brom") has to travel the alias path to get back here.
    /// </summary>
    private static List<RoundMatch> PalpitaoEnglandRound() =>
    [
        Fixture("Arsenal", "Coventry City"),
        Fixture("Hull City", "Manchester United"),
        Fixture("Everton", "Crystal Palace"),
        Fixture("Ipswich Town", "Sunderland"),
        Fixture("Nottingham Forest", "Leeds United"),
        Fixture("Brentford", "Tottenham"),
        Fixture("Brighton & Hove Albion", "Aston Villa"),
        Fixture("Manchester City", "Bournemouth"),
        Fixture("Newcastle", "Liverpool"),
        Fixture("Fulham", "Chelsea"),
        Fixture("Lincoln City", "Portsmouth"),
        Fixture("Millwall", "Norwich City"),
        Fixture("Birmingham City", "Bristol City"),
        Fixture("Blackburn Rovers", "Middlesbrough"),
        Fixture("Derby County", "Cardiff City"),
        Fixture("Preston North End", "Wolverhampton Wanderers"),
        Fixture("Queens Park Rangers", "Bolton Wanderers"),
        Fixture("Southampton", "Stoke City"),
        Fixture("Swansea City", "Sheffield United"),
        Fixture("West Ham United", "Charlton Athletic"),
        Fixture("Wrexham", "Watford"),
        Fixture("West Bromwich Albion", "Burnley"),
        Fixture("Leicester City", "Burton Albion"),
    ];

    [Theory]
    // The group writes the name behind this prefix, in whatever casing it feels like.
    [InlineData("PALPITES Felippe", "Felippe")]
    [InlineData("Palpites do Edson", "Edson")]
    [InlineData("palpite da Ana", "Ana")]
    // ...and often with the round riding on the same line, which is not part of the name.
    [InlineData("PALPITES PL, Rodada 1", "PL")]
    [InlineData("PALPITES Felippe, Rodada 3", "Felippe")]
    [InlineData("Palpites do Edson - Rodada 2", "Edson")]
    public void Parser_reads_the_name_behind_a_palpites_prefix(string header, string expected)
    {
        var parsed = Assert.Single(PureService().Parse($"{header}\nArsenal 2x1 Chelsea"));
        Assert.Equal(expected, parsed.ParticipantName);
    }

    [Theory]
    // WhatsApp's own furniture, sitting right above a block of scores.
    [InlineData("Hoje")]
    [InlineData("Ontem")]
    [InlineData("Palpites")]
    [InlineData("Palpitão")]
    public void Parser_does_not_mistake_the_apps_own_vocabulary_for_the_participant(string noise)
    {
        var parsed = Assert.Single(PureService().Parse($"João\n{noise}\nArsenal 2x1 Chelsea"));
        Assert.Equal("João", parsed.ParticipantName);
    }

    [Theory]
    // The comma is optional and the emphasis markers are stripped...
    [InlineData("*Flavio Rodada 1 (RODADA TESTE", "Flavio")]
    [InlineData("Gilberto, Rodada 2 (1a fase de grupos)", "Gilberto")]
    [InlineData("_Ana_ - Rodada 3", "Ana")]
    // ...but the season title the app itself prints above the fixtures is not a person.
    [InlineData("Palpitão England 2026/2027 — Rodada 1", null)]
    public void Parser_reads_the_round_header_without_taking_the_title_for_a_name(
        string header, string? expected)
    {
        var parsed = Assert.Single(PureService().Parse($"{header}\nArsenal 2x1 Chelsea"));
        Assert.Equal(expected, parsed.ParticipantName);
    }

    [Fact]
    public void Parser_reads_a_score_whose_zeros_ocr_both_returned_as_letters()
    {
        // "O x O" stands alone as its own token, unlike the letters stolen from the ends of two
        // team names in "Arsenal x Leeds" — which stays rejected (see the theory above).
        var parsed = Assert.Single(PureService().Parse("João\nArsenal O x O Chelsea"));

        Assert.Equal(0, parsed.HomeScore);
        Assert.Equal(0, parsed.AwayScore);
        Assert.Equal("Arsenal", parsed.HomeTeamRaw);
        Assert.Equal("Chelsea", parsed.AwayTeamRaw);
    }

    [Theory]
    // The clock WhatsApp stamps on a screenshot, glued to the last fixture of the block. The
    // colon used to read as "Name: content" and swallowed the whole line.
    [InlineData("Cardiff 1x2 Wrexham 17:35 ,")]
    [InlineData("Cardiff 1x 2 Wrexham 01:06 Z")]
    [InlineData("12:32 Cardiff 1x2 Wrexham")]
    public void Parser_ignores_the_clock_stamped_on_the_screenshot(string line)
    {
        var matches = new List<RoundMatch> { Fixture("Cardiff City", "Wrexham") };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\n{line}", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(matches[0].Id, c.RoundMatchId);
        Assert.Equal(1, c.PredictedHomeScore);
        Assert.Equal(2, c.PredictedAwayScore);
    }

    [Theory]
    // One letter wrong in a short name the group message prints. Canonical() is an exact-key
    // lookup, so without the alias bridge "Weolves" never reaches Wolverhampton Wanderers.
    [InlineData("Weolves")]
    [InlineData("Wolvess")]
    public void Parser_tolerates_a_wrong_letter_in_a_short_name(string mangled)
    {
        var matches = new List<RoundMatch> { Fixture("Wolverhampton Wanderers", "Blackburn Rovers") };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"João\n{mangled} 3 x 2 Blackburn", matches, Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(matches[0].Id, c.RoundMatchId);
        Assert.False(c.NeedsReview);
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
    public void Candidate_text_is_truncated_to_what_the_columns_hold()
    {
        // OCR on a noisy screenshot can glue a whole paragraph onto one fixture, and the match
        // text is stored verbatim in a 300-wide column. An over-long insert fails inside the
        // import's try — where the catch that records the failure re-saves the very same tracked
        // entities and fails again, turning a readable "could not read this image" into an
        // opaque 500. (The participant name has its own bound: the parser refuses anything over
        // 40 characters as a name, so that column is only ever defended, never filled.)
        var text = $"João\nArsenal 2 x 1 {new string('b', 400)}";

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), text, Matches(), Participants());

        var c = Assert.Single(candidates);
        Assert.Equal(OcrPredictionCandidate.MaxMatchTextLength, c.MatchTextRaw!.Length);
        Assert.StartsWith("Arsenal 2 x 1 bbb", c.MatchTextRaw, StringComparison.Ordinal);
        Assert.True(c.ParticipantNameRaw!.Length <= OcrPredictionCandidate.MaxParticipantNameLength);
    }

    [Fact]
    public void A_learned_alias_resolves_the_participant_on_the_next_import()
    {
        // "Paraguaio" is the nickname the group writes; the roster says "PL". Nothing about the
        // two strings matches, so only a confirmed alias can bridge them.
        var pl = new User { Id = Guid.NewGuid(), Name = "PL", Role = UserRole.Participant };
        var aliases = new Dictionary<string, Guid> { [OcrTeamMatcher.NormalizeAlias("Paraguaio")] = pl.Id };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "Paraguaio\nArsenal 2x1 Chelsea", Matches(), [pl], aliases);

        var c = Assert.Single(candidates);
        Assert.Equal(pl.Id, c.UserId);
        Assert.False(c.NeedsReview);
        Assert.Equal(1.0, c.Confidence);
    }

    [Theory]
    // The stored key is normalized, so casing, accents and stray spacing all still hit it. An
    // ALL-CAPS line is not among them: the parser refuses to read one as a name at all (that is
    // what keeps OCR noise from stealing the participant), unless it follows "PALPITES".
    [InlineData("paraguaio")]
    [InlineData("Paraguaio")]
    [InlineData("Paraguáio")]
    [InlineData("PALPITES PARAGUAIO")]
    public void A_learned_alias_ignores_casing_and_spacing(string written)
    {
        var pl = new User { Id = Guid.NewGuid(), Name = "PL", Role = UserRole.Participant };
        var aliases = new Dictionary<string, Guid> { [OcrTeamMatcher.NormalizeAlias("  Paraguaio ")] = pl.Id };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), $"{written}\nArsenal 2x1 Chelsea", Matches(), [pl], aliases);

        Assert.Equal(pl.Id, Assert.Single(candidates).UserId);
    }

    [Fact]
    public void A_learned_alias_is_ignored_once_the_participant_leaves_the_roster()
    {
        var gone = Guid.NewGuid();
        var aliases = new Dictionary<string, Guid> { [OcrTeamMatcher.NormalizeAlias("Paraguaio")] = gone };

        var candidates = PureService().BuildCandidates(
            Guid.NewGuid(), Guid.NewGuid(), "Paraguaio\nArsenal 2x1 Chelsea", Matches(), Participants(), aliases);

        var c = Assert.Single(candidates);
        Assert.Null(c.UserId);
        Assert.True(c.NeedsReview);
    }

    [Fact]
    public async Task Confirm_learns_the_name_the_admin_had_to_correct()
    {
        using var db = CreateContext();
        var (roundId, matchId, _) = SeedRound(db);
        var pl = SeedParticipant(db, "PL");
        var batchId = SeedBatchWithCandidates(db, roundId, (matchId, pl, "Paraguaio"));
        var service = ImportService(db);

        await service.ConfirmAsync(batchId, Admin, Ct);

        var alias = await db.OcrParticipantAliases.SingleAsync();
        Assert.Equal(OcrTeamMatcher.NormalizeAlias("Paraguaio"), alias.Alias);
        Assert.Equal("Paraguaio", alias.AliasRaw);
        Assert.Equal(pl, alias.UserId);
        Assert.Equal(SeedIds.DefaultGroup, alias.GroupId);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.Action == "OcrParticipantAliasesLearned");
    }

    [Fact]
    public async Task Confirm_does_not_learn_a_name_the_matcher_already_reads()
    {
        // Nothing was corrected here, so there is nothing to remember — and storing it would
        // pin a roster name that is free to change.
        using var db = CreateContext();
        var (roundId, matchId, _) = SeedRound(db);
        var pl = SeedParticipant(db, "PL");
        var batchId = SeedBatchWithCandidates(db, roundId, (matchId, pl, "PL"));
        var service = ImportService(db);

        await service.ConfirmAsync(batchId, Admin, Ct);

        Assert.Empty(await db.OcrParticipantAliases.ToListAsync());
    }

    [Fact]
    public async Task Confirm_does_not_learn_a_name_two_participants_shared()
    {
        // One image can carry two people. A name filed against both is a coin flip next time,
        // so it is better forgotten than guessed.
        using var db = CreateContext();
        var (roundId, matchId, _) = SeedRound(db);
        var first = SeedParticipant(db, "PL");
        var second = SeedParticipant(db, "Flavio");
        var batchId = SeedBatchWithCandidates(
            db, roundId, (matchId, first, "Paraguaio"), (matchId, second, "Paraguaio"));
        var service = ImportService(db);

        await service.ConfirmAsync(batchId, Admin, Ct);

        Assert.Empty(await db.OcrParticipantAliases.ToListAsync());
    }

    [Fact]
    public async Task Confirm_repoints_an_alias_that_turned_out_to_belong_to_someone_else()
    {
        // An alias learned from OCR junk must be correctable: the newest confirmation wins
        // rather than leaving the group with a permanently wrong mapping.
        using var db = CreateContext();
        var (roundId, matchId, _) = SeedRound(db);
        var wrong = SeedParticipant(db, "PL");
        var right = SeedParticipant(db, "Flavio");
        var service = ImportService(db);

        await service.ConfirmAsync(SeedBatchWithCandidates(db, roundId, (matchId, wrong, "nAc")), Admin, Ct);
        await service.ConfirmAsync(SeedBatchWithCandidates(db, roundId, (matchId, right, "nAc")), Admin, Ct);

        var alias = await db.OcrParticipantAliases.SingleAsync();
        Assert.Equal(right, alias.UserId);
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

    /// <summary>An approved, active participant of the default group — what the alias learning
    /// reads through <c>GroupQueries.ActiveParticipants</c>, so the plain User row is not enough.</summary>
    private static Guid SeedParticipant(AppDbContext db, string name)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"{id}@x.com",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = SeedIds.DefaultGroup,
            UserId = id,
            Role = GroupRole.Participant,
            Status = GroupUserStatus.Approved,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    /// <summary>A reviewed batch whose candidates are already filed against a participant —
    /// the state the admin leaves behind just before pressing Confirm.</summary>
    private static Guid SeedBatchWithCandidates(
        AppDbContext db, Guid roundId, params (Guid MatchId, Guid UserId, string Raw)[] candidates)
    {
        var batch = new OcrImportBatch
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            UploadedByUserId = Admin,
            OriginalFileName = "x.png",
            LanguageUsed = "por",
            Status = OcrBatchStatus.Reviewed,
            CreatedAt = DateTime.UtcNow,
        };
        db.OcrImportBatches.Add(batch);
        foreach (var (matchId, userId, raw) in candidates)
        {
            db.OcrPredictionCandidates.Add(new OcrPredictionCandidate
            {
                Id = Guid.NewGuid(),
                OcrImportBatchId = batch.Id,
                RoundId = roundId,
                UserId = userId,
                ParticipantNameRaw = raw,
                RoundMatchId = matchId,
                PredictedHomeScore = 2,
                PredictedAwayScore = 1,
                NeedsReview = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        db.SaveChanges();
        return batch.Id;
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
        var service = ImportService(db);

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
        var service = ImportService(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(batchId, Admin, Ct));
    }

    [Fact]
    public async Task Confirm_fails_when_already_confirmed()
    {
        using var db = CreateContext();
        var (roundId, matchId, userId) = SeedRound(db);
        var batchId = SeedBatch(db, roundId, matchId, userId, 2, 1);
        var service = ImportService(db);
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
        var service = ImportService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(batchId, Admin, Ct));
        Assert.Equal("ocr.duplicateCandidates", ex.Key);
        Assert.Empty(await db.Predictions.ToListAsync());
    }
}
