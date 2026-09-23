using System.Text;
using System.Text.RegularExpressions;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Pure OCR text → prediction-line parsing: tolerant score/team/name recognition
/// and cleanup of flag-glyph / emoji junk that OCR injects. No database, no entity
/// matching — that lives in <see cref="OcrTeamMatcher"/>.
/// </summary>
public static partial class OcrTextParser
{
    // A score pair "d x d" found anywhere in a line. Single OCR-tolerant digit
    // on each side (letters like O→0, I/l→1, S→5, B→8 are canonicalised by
    // ParseScore). Matching anywhere lets us cope with flag/emoji junk that OCR
    // injects around the score (e.g. "Bélgica R 2x 1 == Egito"). The separator may come back
    // doubled ("Millwall 2xX1 Norwich"), which is one glyph read twice, not two scores.
    [GeneratedRegex(@"([0-9OoQqDIiLlSsBbZzGg])\s*[xX×:\-–]{1,2}\s*([0-9OoQqDIiLlSsBbZzGg])")]
    private static partial Regex ScorePair();

    // "Arsenal 2 Chelsea 1", "Liverpool 1 City 1" (no separator, fallback).
    [GeneratedRegex(@"^(.+?)\s+([0-9OoQqDIiLlSsBbZzGg]{1,2})\s+(.+?)\s+([0-9OoQqDIiLlSsBbZzGg]{1,2})$")]
    private static partial Regex TeamScoreTeamScore();

    // "Pedro - Arsenal 2 Chelsea 1" (name, then content)
    [GeneratedRegex(@"^(.+?)\s+-\s+(.+)$")]
    private static partial Regex NameDashContent();

    // Flags/emoji/pictographs and other symbol/format noise (WhatsApp screenshots,
    // e.g. "Belgica [flag] 2 x 1 [flag] Egito"). Includes the variation selectors
    // (U+FE00–U+FE0F) that follow emoji.
    [GeneratedRegex(@"[\p{So}\p{Sk}\p{Cs}\p{Cf}\p{Co}︀-️]")]
    private static partial Regex SymbolNoise();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    // The clock WhatsApp stamps on every screenshot ("17:35", "01:06 Z", the status bar's
    // "12:32"). Two digits after the colon, so a "2:1" score is left alone. Removed before
    // anything else looks at the line: SplitNameAndContent treats a colon as "Name: content",
    // so a trailing timestamp otherwise swallows the whole fixture line.
    [GeneratedRegex(@"(?<!\d)\d{1,2}:\d{2}(?!\d)")]
    private static partial Regex ClockTime();

    // Header with the participant's name, e.g. "Gilberto, Rodada 2 (1a fase de grupos)" or
    // "*Flavio Rodada 1 (RODADA TESTE". The separator is optional: WhatsApp bold markers and
    // OCR both eat the comma often enough that requiring it loses the name outright.
    //
    // The keyword itself gets one wrong character, spelled out rather than fuzzy-matched because
    // it is a fixed six-letter word: OCR returned "Feunppe, Kodada 1" for a real header, and a
    // capital R read as a K threw the whole name away. "odada" anchors it — nothing else spells
    // that, so the extra room costs nothing.
    [GeneratedRegex(
        @"^(.+?)\s*[,\-–—]?\s*(?:rodada|\wodada|r\wdada|ro\wada|rod\wda|roda\wa|rodad\w)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ParticipantHeader();

    // "PALPITES Felippe", "Palpites do Edson", "PALPITES PL" — the line the group actually
    // writes above a block of scores. The name after the prefix is taken as-is (it is often
    // ALL-CAPS, which LooksLikeName alone rejects).
    [GeneratedRegex(@"^palpites?\s+(?:d[oea]\s+)?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PredictionsHeader();

    // WhatsApp emphasis markers and quotes wrapped around a name: *Flavio*, _Edson_, "Paraguaio".
    [GeneratedRegex(@"[*_~""“”'‘’]")]
    private static partial Regex NameDecoration();

    // The "Nome" placeholder the round message prints where the participant writes their name.
    [GeneratedRegex(@"^nome\b[\s,.;:\-–—]*", RegexOptions.IgnoreCase)]
    private static partial Regex TemplatePlaceholder();

    // A phone number WhatsApp prints next to a sender who is not a contact
    // ("~Gilberto Sales +55 85 98934-0476"). Its "4-0476" otherwise reads as a 4-0 score.
    [GeneratedRegex(@"(?:\+\d{1,3}\s*)?(?:\(?\d{2}\)?\s*)?(?<!\d)\d{4,5}\s?-\s?\d{4}(?!\d)")]
    private static partial Regex PhoneNumber();

    // A zero OCR returned twice, once as the letter and once as the digit, right against the
    // separator: "Bolton OxO0 Cardiff", "Tottenham O0x0O Aston Villa". Left alone, the doubled
    // glyph fails the score's own shape check and the fixture is lost.
    [GeneratedRegex(@"(?<![0-9\p{L}])[0Oo]([0Oo])(?=\s*[xX×:\-–])")]
    private static partial Regex DoubledZeroBeforeSeparator();

    /// <inheritdoc cref="DoubledZeroBeforeSeparator"/>
    [GeneratedRegex(@"(?<=[xX×:\-–]\s*)[0Oo]([0Oo])(?![0-9\p{L}])")]
    private static partial Regex DoubledZeroAfterSeparator();

    [GeneratedRegex(@"[\s_\-.+()\[\]]+")]
    private static partial Regex FileNameSeparators();

    [GeneratedRegex(@"(?<=\p{Ll})(?=\p{Lu})")]
    private static partial Regex CamelCaseBoundary();

    public static IReadOnlyList<ParsedPrediction> Parse(string text)
    {
        var result = new List<ParsedPrediction>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        string? currentName = null;
        var announced = false;
        foreach (var line in ReadLines(text))
        {
            // "Name: content" / "Name - content"
            var (name, content) = SplitNameAndContent(line);
            if (name is not null && content is not null)
            {
                currentName = name;
                foreach (var segment in content.Split(','))
                {
                    var parsed = TryParseMatch(segment.Trim());
                    if (parsed is not null)
                    {
                        // The label sits on the fixture's own line, which names the participant
                        // as plainly as a header does.
                        result.Add(parsed with { ParticipantName = currentName, ParticipantAnnounced = true });
                    }
                }
                continue;
            }

            // Pure match line -> uses the current participant context.
            var match = TryParseMatch(line);
            if (match is not null)
            {
                result.Add(match with { ParticipantName = currentName, ParticipantAnnounced = announced });
                continue;
            }

            // Otherwise it may be a participant name. "PALPITES <nome>" and "<Nome>, Rodada N"
            // announce one outright; a plain clean name is only taken until one of those does.
            // Garbled OCR lines (symbols, digits, ALL-CAPS noise) are ignored so they do not
            // steal the participant context from the real matches.
            if (TryReadParticipantName(line) is { } participant
                && (participant.Announced || !announced))
            {
                // A bare line never overrides a name the message announced outright. OCR turns
                // headings and club names into something name-shaped ("Championshio", "Leaque
                // One", "Bristol City"), and letting one of those win costs every fixture below
                // it -- and, on confirm, teaches the group a junk alias for a club.
                currentName = participant.Name;
                announced |= participant.Announced;
            }
        }

        return result;
    }

    /// <summary>
    /// Cleans every line and re-joins a fixture the chat bubble broke in two
    /// ("Birmingham 2 x 0" / "Bristol City"). The orphan half is not merely a lost fixture: it is
    /// name-shaped, so it becomes the participant and every row below it is filed against a club.
    /// Only the away side is looked for below, which is the way a bubble wraps: the tail of the
    /// line is what moves down.
    /// </summary>
    private static List<string> ReadLines(string text)
    {
        var lines = text.Replace("\r", string.Empty)
            .Split('\n')
            .Select(CleanLine)
            .Where(l => l.Length > 0)
            .ToList();

        var joined = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            // Checked, not assumed: the halves are glued together only when the result really
            // reads as a fixture, so a stray score above an unrelated line leaves both alone.
            if (i + 1 < lines.Count
                && HasDanglingScore(lines[i])
                && CanCompleteFixture(lines[i + 1])
                && TryParseMatch($"{lines[i]} {lines[i + 1]}") is not null)
            {
                joined.Add($"{lines[i]} {lines[i + 1]}");
                i++;
                continue;
            }

            joined.Add(lines[i]);
        }

        return joined;
    }

    /// <summary>
    /// True when a line carries a real score and a home team but no away team: the top half of a
    /// fixture the bubble wrapped. A line that already parses whole is never a candidate.
    /// </summary>
    private static bool HasDanglingScore(string line)
    {
        if (TryParseMatch(line) is not null)
        {
            return false;
        }

        foreach (Match pair in ScorePair().Matches(line))
        {
            if (IsRealScore(line, pair) && CleanTeam(line[..pair.Index]).Length >= 2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the line carries a score of its own, wherever it sits.</summary>
    private static bool HasScore(string line) =>
        ScorePair().Matches(line).Any(pair => IsRealScore(line, pair));

    /// <summary>A matched pair that is really a score: it is a pair, and both sides parse.</summary>
    private static bool IsRealScore(string line, Match pair) =>
        IsScorePair(line, pair)
        && ParseScore(pair.Groups[1].Value) is not null
        && ParseScore(pair.Groups[2].Value) is not null;

    /// <summary>
    /// True when a line may be the tail of the fixture above it: the top half carries the score,
    /// so the bottom half must carry none. Without that rule a line OCR left without its away
    /// team swallows the next fixture whole, costing a good row and inventing a wrong one. A
    /// competition heading or a name header announces the next block instead, and swallowing one
    /// would cost the participant every row below it -- the very thing this pass exists to stop.
    /// </summary>
    private static bool CanCompleteFixture(string line) =>
        !HasScore(line)
        && !PredictionsHeader().IsMatch(line)
        && !ParticipantHeader().IsMatch(line)
        && !IsCompetitionHeading(NormalizeName(line));

    /// <summary>
    /// A participant a line names, and whether the line announced it outright (the "PALPITES"
    /// prefix or the round header) rather than merely being name-shaped.
    /// </summary>
    private readonly record struct ParticipantLine(string Name, bool Announced);

    /// <summary>
    /// Reads the participant a line names, or null when the line is not a name. Tried in
    /// order of how strong the signal is: the "PALPITES &lt;nome&gt;" prefix the group writes, the
    /// "&lt;Nome&gt;, Rodada N" header the app's own message prints, then a bare clean name.
    /// </summary>
    private static ParticipantLine? TryReadParticipantName(string line)
    {
        var predictions = PredictionsHeader().Match(line);
        if (predictions.Success)
        {
            // The prefix already establishes that a name follows, so an ALL-CAPS one is fine
            // ("PALPITES PL"): only the shape is checked, not the casing. The round rides on
            // that same line often enough ("PALPITES PL, Rodada 1") that it has to come off
            // first: left in, its comma and digit fail the shape check and the header is lost.
            var candidate = NormalizeName(StripRoundTail(predictions.Groups[1].Value));
            return IsNameShaped(candidate) ? new ParticipantLine(candidate, true) : null;
        }

        var header = ParticipantHeader().Match(line);
        if (header.Success)
        {
            // Checked, not trusted: "Palpitão England 2026/2027 — Rodada 1" also matches the
            // header, and filing every fixture against a season title helps nobody. Initials are
            // the exception to the lowercase rule ("JP Rodada 9", "PL Rodada 8"): the header has
            // already said a name comes first, and a lone short token cannot be a title.
            var candidate = StripPlaceholder(NormalizeName(header.Groups[1].Value));
            return LooksLikeName(candidate) || IsInitials(candidate)
                ? new ParticipantLine(candidate, true)
                : null;
        }

        var plain = NormalizeName(line);
        return LooksLikeName(plain) ? new ParticipantLine(plain, false) : null;
    }

    /// <summary>
    /// Drops a "... Rodada N" tail from a name, through the very regex that reads the round header
    /// so where the round starts stays defined in one place. Returns the value untouched when the
    /// line carries no round.
    /// </summary>
    private static string StripRoundTail(string value)
    {
        var header = ParticipantHeader().Match(value);
        return header.Success ? header.Groups[1].Value : value;
    }

    /// <summary>Strips the WhatsApp emphasis markers and quotes that wrap a name.</summary>
    private static string NormalizeName(string value) =>
        WhitespaceRun().Replace(NameDecoration().Replace(value, " "), " ").Trim();

    /// <summary>
    /// Drops the "Nome" placeholder the round message prints where the participant's name goes,
    /// which people leave in front of their name ("Nome, Defarias, Rodada 9") or do not replace at
    /// all ("Nome, Rodada 5" — then nothing is left, and no name is read).
    /// </summary>
    private static string StripPlaceholder(string name)
    {
        var placeholder = TemplatePlaceholder().Match(name);
        return placeholder.Success ? name[placeholder.Length..].Trim() : name;
    }

    /// <summary>A participant who signs with initials: one all-caps token of 2–4 letters.</summary>
    private static bool IsInitials(string name) =>
        name.Length is >= 2 and <= 4 && name.All(char.IsUpper) && IsNameShaped(name);

    /// <summary>
    /// A competition heading, allowing for what OCR does to one ("Premier Leaque", "FPremies
    /// League", "Chempionship"): the long headings absorb two wrong characters, the short one one.
    /// None of them is anywhere near a person's name, so the tolerance costs nothing.
    /// </summary>
    private static bool IsCompetitionHeading(string line)
    {
        if (CompetitionHeadings.Contains(line))
        {
            return true;
        }

        var folded = OcrTeamMatcher.Fold(line);
        return CompetitionHeadings.Any(heading =>
            OcrTeamMatcher.WithinDistance(folded, OcrTeamMatcher.Fold(heading), heading.Length >= 10 ? 2 : 1));
    }

    /// <summary>
    /// True when a name is worth remembering as a participant alias: shaped like a name and not a
    /// word of the round message itself. Guards the alias learning, which is silent and trusted —
    /// a "REGRA FLÁVIO" or "palpitar" learned there would file someone else's predictions later.
    /// </summary>
    public static bool IsPlausibleParticipantName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && IsNameShaped(NormalizeName(name));

    /// <summary>
    /// The participant a screenshot's file name points at: admins name each print after its
    /// participant ("Valter.png", "Valter1.jpeg", "DeFarias.jpeg", "ezau4e5.jpeg"). Returns the
    /// name part — cut at the first digit, separators turned into spaces, camel case split — or
    /// null when the name says nothing about a person ("9.png", "IMG_2041.jpg",
    /// "WhatsApp Image 2026-09-18 at 14.42.jpeg").
    /// </summary>
    public static string? NameFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        // Composed first: iOS and macOS hand file names over with accents as separate marks.
        var stem = Path.GetFileNameWithoutExtension(fileName.Trim()).Normalize(NormalizationForm.FormC);
        var digit = stem.IndexOfAny(Digits);
        if (digit >= 0)
        {
            stem = stem[..digit];
        }

        stem = WhitespaceRun().Replace(FileNameSeparators().Replace(stem, " "), " ").Trim();

        // Generic words are looked for before the camel case is split as well as after: split
        // first, "WhatsApp" becomes "Whats App" and slips through as a name.
        var words = CamelCaseBoundary().Replace(stem, " ");
        words = WhitespaceRun().Replace(words, " ").Trim();

        return IsNameShaped(words)
            && !stem.Split(' ').Concat(words.Split(' ')).Any(GenericFileWords.Contains)
                ? words
                : null;
    }

    private static readonly char[] Digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    /// <summary>
    /// Words that file names carry but no participant is called: what phones, chat apps and
    /// browsers name an image, and the words an admin adds to one.
    /// </summary>
    private static readonly HashSet<string> GenericFileWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "img", "image", "imagem", "imagens", "pic", "picture", "foto", "photo", "scan", "file", "arquivo",
        "whatsapp", "telegram", "signal", "screenshot", "screen", "shot", "captura", "tela", "print",
        "printscreen", "recorte", "crop", "cropped", "edited", "editada", "copy", "copia", "cópia",
        "download", "unnamed", "palpite", "palpites", "rodada",
    };

    /// <summary>
    /// Competition headings the copy-ready message prints above each block of fixtures
    /// (mirrors COMP_LABEL in the frontend's shared/utils/round-message.util.ts). They are
    /// indistinguishable from a participant name by shape — letters and spaces, title case —
    /// so without this "Premier League" is read as the participant and every fixture under it
    /// is filed against a person who does not exist.
    /// </summary>
    private static readonly HashSet<string> CompetitionHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "Premier League",
        "Championship",
        "FA Cup",
        "League One",
        "Copa do Mundo FIFA",
        "FIFA World Cup",
    };

    /// <summary>
    /// Words that are name-shaped but never a person: the app's own vocabulary and the day
    /// separators WhatsApp prints between messages. Without this, "Hoje" sits right above a
    /// block of scores and quietly becomes the participant they are filed against. Checked word
    /// by word, so "REGRA FLÁVIO" and "Palpitão England" are caught as well as "Palpites"; the
    /// wrapped tail of the Flávio rule line leaves "palpitar" alone on a line, and "Nome" is the
    /// placeholder the message prints for the participant's name.
    /// </summary>
    private static readonly HashSet<string> NoiseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Palpite", "Palpites", "Palpitão", "Palpitao", "Palpitar", "FanPicks",
        "Hoje", "Ontem", "Today", "Yesterday",
        "Mensagem", "Message", "Regra", "Regras", "Rules", "Nome", "Rodada",
    };

    /// <summary>
    /// True for plain participant names ("João", "Pedro Silva"): name-shaped and with at least
    /// one lowercase letter. The casing rule is what keeps ALL-CAPS OCR noise from stealing the
    /// participant context — it is dropped only after an explicit "PALPITES" prefix, which has
    /// already established that a name follows.
    /// </summary>
    private static bool LooksLikeName(string line) =>
        IsNameShaped(line) && line.Any(char.IsLower);

    /// <summary>
    /// Shape check shared by both name paths: letters and spaces only, 2–40 characters, up to
    /// four words, and not one of the headings/vocabulary words that are name-shaped by accident.
    /// </summary>
    private static bool IsNameShaped(string line)
    {
        if (line.Length is < 2 or > 40
            || line.Split(' ').Any(NoiseNames.Contains)
            || IsCompetitionHeading(line))
        {
            return false;
        }

        var words = 1;
        foreach (var c in line)
        {
            if (c == ' ')
            {
                words++;
                continue;
            }

            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return words <= 4;
    }

    /// <summary>
    /// Removes OCR junk (flag glyphs read as stray letters/symbols) from a team
    /// name in three steps: trim boundary non-letters; de-glue each token (drop
    /// fragments fused onto the name, e.g. "gRD"→"RD", "ak=Noruega"→"Noruega");
    /// then drop leftover junk tokens from the edges. Keeps legitimate multi-word
    /// names like "Cabo Verde", "Arábia Saudita" and "RD Congo".
    /// </summary>
    private static string CleanTeam(string raw)
    {
        var s = raw.Trim();
        int start = 0, end = s.Length;
        while (start < end && !char.IsLetter(s[start]))
        {
            start++;
        }

        while (end > start && !char.IsLetter(s[end - 1]))
        {
            end--;
        }

        s = s[start..end];
        if (s.Length == 0)
        {
            return s;
        }

        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(DeGlue)
            .Where(t => t.Length > 0)
            .ToList();
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        // Only trim edges when a confident real word anchors the name, so we never
        // strip a name down to nothing (or to the junk) when OCR mangled it badly.
        if (tokens.Any(t => IsConfidentWord(t, leading: true) || IsConfidentWord(t, leading: false)))
        {
            while (tokens.Count > 1 && !IsConfidentWord(tokens[0], leading: true))
            {
                tokens.RemoveAt(0);
            }

            while (tokens.Count > 1 && !IsConfidentWord(tokens[^1], leading: false))
            {
                tokens.RemoveAt(tokens.Count - 1);
            }
        }

        return string.Join(' ', tokens);
    }

    /// <summary>
    /// Splits a token at non-letters (except hyphen/apostrophe) and at lowercase→
    /// uppercase boundaries, then keeps the longest fragment. This peels flag-glyph
    /// junk fused onto a name ("gRD"→"RD", "BlArgélia"→"Argélia", "ak=Noruega"→
    /// "Noruega") without touching clean tokens ("Congo", "RD", "Zelândia").
    /// </summary>
    private static string DeGlue(string token)
    {
        var fragments = new List<string>();
        var current = new System.Text.StringBuilder();
        char? prev = null;
        foreach (var c in token)
        {
            var isNameChar = char.IsLetter(c) || c is '-' or '\'';
            if (!isNameChar)
            {
                Flush();
                prev = null;
                continue;
            }

            if (prev is { } p && char.IsLower(p) && char.IsUpper(c))
            {
                Flush();
            }

            current.Append(c);
            prev = c;
        }

        Flush();
        return fragments.Count == 0 ? string.Empty : fragments.MaxBy(f => f.Length)!;

        void Flush()
        {
            if (current.Length > 0)
            {
                fragments.Add(current.ToString());
                current.Clear();
            }
        }
    }

    /// <summary>
    /// A token we trust as a real team word: a Title-case word ("Congo", "Zelândia")
    /// or, only in leading position, a short all-caps abbreviation ("RD").
    /// </summary>
    private static bool IsConfidentWord(string t, bool leading)
    {
        if (t.Length < 2 || t.Any(c => !char.IsLetter(c)))
        {
            return false;
        }

        if (char.IsUpper(t[0]) && t.Skip(1).Any(char.IsLower))
        {
            return true;
        }

        return leading && t.Length <= 4 && t.All(char.IsUpper);
    }

    /// <summary>
    /// Normalises an OCR/clipboard line: strips emoji/flags/symbol noise and
    /// collapses runs of whitespace.
    /// </summary>
    private static string CleanLine(string raw)
    {
        var withoutSymbols = SymbolNoise().Replace(raw, " ");
        var withoutClock = ClockTime().Replace(withoutSymbols, " ");
        var withoutPhone = PhoneNumber().Replace(withoutClock, " ");
        var singleZeros = DoubledZeroAfterSeparator().Replace(
            DoubledZeroBeforeSeparator().Replace(withoutPhone, "$1"), "$1");
        return WhitespaceRun().Replace(singleZeros, " ").Trim();
    }

    private static (string? Name, string? Content) SplitNameAndContent(string line)
    {
        // Only when what follows the colon really holds a fixture. A digit alone is not enough:
        // the round message's own "REGRA FLÁVIO: @Bruno tem até 24 horas" line then named the
        // participant of every fixture below it, and OCR turning a "(×2)" tag into "(:2)" made
        // "Luton 1 x O Bradford (:2)" a name with no fixture at all.
        // The label itself must not hold a score, or "Luton 1 x 0 Bradford - Arsenal 2 x 1 Chelsea"
        // would file a fixture under a participant called "Luton 1 x 0 Bradford".
        var colon = line.IndexOf(':');
        if (colon > 0 && !HasScore(line[..colon]))
        {
            var content = line[(colon + 1)..].Trim();
            if (content.Split(',').Any(segment => TryParseMatch(segment.Trim()) is not null))
            {
                return (line[..colon].Trim(), content);
            }
        }

        var dash = NameDashContent().Match(line);
        if (dash.Success
            && !HasScore(dash.Groups[1].Value)
            && TryParseMatch(dash.Groups[2].Value.Trim()) is not null)
        {
            return (dash.Groups[1].Value.Trim(), dash.Groups[2].Value.Trim());
        }

        return (null, null);
    }

    private static ParsedPrediction? TryParseMatch(string text)
    {
        // Every score pair is tried, not just the first: OCR noise ahead of the real score
        // ("Cardiff 1x 3 Wrexham 01 06 Z" once the clock is stripped, "B * SE" glued to a line)
        // otherwise consumes the only attempt and the whole fixture is lost.
        foreach (Match a in ScorePair().Matches(text))
        {
            if (!IsScorePair(text, a)
                || ParseScore(a.Groups[1].Value) is not { } homeA
                || ParseScore(a.Groups[2].Value) is not { } awayA)
            {
                continue;
            }

            var home = CleanTeam(text[..a.Index]);
            var away = CleanTeam(text[(a.Index + a.Length)..]);
            if (home.Length >= 2 && away.Length >= 2)
            {
                return new ParsedPrediction(null, text, home, homeA, away, awayA,
                    ScoreFromLetter: IsGuessedDigit(a.Groups[1].Value) || IsGuessedDigit(a.Groups[2].Value));
            }
        }

        var b = TeamScoreTeamScore().Match(text);
        if (b.Success
            && ParseScore(b.Groups[2].Value) is { } homeB
            && ParseScore(b.Groups[4].Value) is { } awayB)
        {
            var home = CleanTeam(b.Groups[1].Value);
            var away = CleanTeam(b.Groups[3].Value);
            if (home.Length >= 2 && away.Length >= 2)
            {
                return new ParsedPrediction(null, text, home, homeB, away, awayB,
                    ScoreFromLetter: IsGuessedDigit(b.Groups[2].Value) || IsGuessedDigit(b.Groups[4].Value));
            }
        }

        return null;
    }

    /// <summary>
    /// True when a score token holds a letter other than O. An O for a zero is what OCR returns all
    /// the time and is never anything else; the other look-alikes are guesses — the "b" of
    /// "Man City bxO0 Coventry" was a 5 on the screenshot, not the 8 <see cref="ParseScore"/> maps it
    /// to — so a score read that way goes to review instead of being trusted.
    /// </summary>
    private static bool IsGuessedDigit(string token) =>
        token.Any(c => char.IsLetter(c) && c is not ('O' or 'o'));

    /// <summary>
    /// True when a matched pair is really a score and not two team names around the message
    /// template's own separator. A line returned unedited ("Arsenal x Leeds") otherwise reads
    /// as "Arsena" 1 x 1 "eeds" — both halves still resolve, so a made-up 1x1 would be imported
    /// without review.
    ///
    /// A real digit on either side settles it. Failing that, the pair must stand alone as its own
    /// token ("Norwich O x O West Brom", where OCR read both zeros as the letter O): the
    /// characters flanking it are then spaces or the line's edge, whereas the letters stolen from
    /// "Arsenal"/"Leeds" are glued to the rest of their word.
    /// </summary>
    private static bool IsScorePair(string text, Match pair)
    {
        var before = pair.Index - 1;
        var after = pair.Index + pair.Length;

        // Across a colon or a dash, a digit glued to another digit belongs to a longer number — a
        // time OCR mangled ("22:897"), a phone number, a date — and read as a score it becomes a row
        // of junk the admin has to delete. Not across an "x": that is the template's own separator,
        // and OCR does glue a stray digit to a real score there ("Irã sS 1 x 07* Nova Zelândia").
        if (pair.Value.Any(c => c is ':' or '-' or '–')
            && ((before >= 0 && char.IsAsciiDigit(text[before]))
                || (after < text.Length && char.IsAsciiDigit(text[after]))))
        {
            return false;
        }

        if (pair.Groups[1].Value.Any(char.IsAsciiDigit) || pair.Groups[2].Value.Any(char.IsAsciiDigit))
        {
            return true;
        }
        return (before < 0 || !IsWordChar(text[before]))
            && (after >= text.Length || !IsWordChar(text[after]));

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c);
    }

    /// <summary>
    /// Canonicalises a score token, mapping common OCR letter look-alikes to
    /// digits (O/Q/D→0, I/L→1, Z→2, S→5, G→6, B→8). Returns null if the token
    /// is not a plausible 0–2 digit score after normalisation.
    /// </summary>
    private static int? ParseScore(string token)
    {
        Span<char> buffer = stackalloc char[token.Length];
        var len = 0;
        foreach (var ch in token)
        {
            var mapped = char.ToUpperInvariant(ch) switch
            {
                >= '0' and <= '9' => ch,
                'O' or 'Q' or 'D' => '0',
                'I' or 'L' => '1',
                'Z' => '2',
                'S' => '5',
                'G' => '6',
                'B' => '8',
                _ => '\0',
            };
            if (mapped == '\0')
            {
                return null;
            }

            buffer[len++] = mapped;
        }

        return len > 0 && int.TryParse(buffer[..len], out var value) ? value : null;
    }
}
