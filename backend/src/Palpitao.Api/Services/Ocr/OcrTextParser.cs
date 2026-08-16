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
    // injects around the score (e.g. "Bélgica R 2x 1 == Egito").
    [GeneratedRegex(@"([0-9OoQqDIiLlSsBbZzGg])\s*[xX×:\-–]\s*([0-9OoQqDIiLlSsBbZzGg])")]
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
    [GeneratedRegex(@"^(.+?)\s*[,\-–—]?\s*rodada\b", RegexOptions.IgnoreCase)]
    private static partial Regex ParticipantHeader();

    // "PALPITES Felippe", "Palpites do Edson", "PALPITES PL" — the line the group actually
    // writes above a block of scores. The name after the prefix is taken as-is (it is often
    // ALL-CAPS, which LooksLikeName alone rejects).
    [GeneratedRegex(@"^palpites?\s+(?:d[oea]\s+)?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PredictionsHeader();

    // WhatsApp emphasis markers and quotes wrapped around a name: *Flavio*, _Edson_, "Paraguaio".
    [GeneratedRegex(@"[*_~""“”'‘’]")]
    private static partial Regex NameDecoration();

    public static IReadOnlyList<ParsedPrediction> Parse(string text)
    {
        var result = new List<ParsedPrediction>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        string? currentName = null;
        foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = CleanLine(rawLine);
            if (line.Length == 0)
            {
                continue;
            }

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
                        result.Add(parsed with { ParticipantName = currentName });
                    }
                }
                continue;
            }

            // Pure match line -> uses the current participant context.
            var match = TryParseMatch(line);
            if (match is not null)
            {
                result.Add(match with { ParticipantName = currentName });
                continue;
            }

            // Otherwise it may be a participant name. "PALPITES <nome>" and "<Nome>, Rodada N"
            // are strong signals; a plain clean name overrides too. Garbled OCR lines (symbols,
            // digits, ALL-CAPS noise) are ignored so they do not steal the participant context
            // from the real matches.
            if (TryReadParticipantName(line) is { } participant)
            {
                currentName = participant;
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the participant a line announces, or null when the line is not a name. Tried in
    /// order of how strong the signal is: the "PALPITES &lt;nome&gt;" prefix the group writes, the
    /// "&lt;Nome&gt;, Rodada N" header the app's own message prints, then a bare clean name.
    /// </summary>
    private static string? TryReadParticipantName(string line)
    {
        var predictions = PredictionsHeader().Match(line);
        if (predictions.Success)
        {
            // The prefix already establishes that a name follows, so an ALL-CAPS one is fine
            // ("PALPITES PL"): only the shape is checked, not the casing.
            var candidate = NormalizeName(predictions.Groups[1].Value);
            return IsNameShaped(candidate) ? candidate : null;
        }

        var header = ParticipantHeader().Match(line);
        if (header.Success)
        {
            // Checked, not trusted: "Palpitão England 2026/2027 — Rodada 1" also matches the
            // header, and filing every fixture against a season title helps nobody.
            var candidate = NormalizeName(header.Groups[1].Value);
            return LooksLikeName(candidate) ? candidate : null;
        }

        var plain = NormalizeName(line);
        return LooksLikeName(plain) ? plain : null;
    }

    /// <summary>Strips the WhatsApp emphasis markers and quotes that wrap a name.</summary>
    private static string NormalizeName(string value) =>
        WhitespaceRun().Replace(NameDecoration().Replace(value, " "), " ").Trim();

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
    /// block of scores and quietly becomes the participant they are filed against.
    /// </summary>
    private static readonly HashSet<string> NoiseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Palpite", "Palpites", "Palpitão", "Palpitao", "FanPicks",
        "Hoje", "Ontem", "Today", "Yesterday",
        "Mensagem", "Message", "Regras", "Rules",
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
            || CompetitionHeadings.Contains(line)
            || NoiseNames.Contains(line))
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
        return WhitespaceRun().Replace(withoutClock, " ").Trim();
    }

    private static (string? Name, string? Content) SplitNameAndContent(string line)
    {
        var colon = line.IndexOf(':');
        if (colon > 0 && line[(colon + 1)..].Any(char.IsDigit))
        {
            return (line[..colon].Trim(), line[(colon + 1)..].Trim());
        }

        var dash = NameDashContent().Match(line);
        if (dash.Success && TryParseMatch(dash.Groups[2].Value.Trim()) is not null)
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
                return new ParsedPrediction(null, text, home, homeA, away, awayA);
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
                return new ParsedPrediction(null, text, home, homeB, away, awayB);
            }
        }

        return null;
    }

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
        if (pair.Groups[1].Value.Any(char.IsAsciiDigit) || pair.Groups[2].Value.Any(char.IsAsciiDigit))
        {
            return true;
        }

        var before = pair.Index - 1;
        var after = pair.Index + pair.Length;
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
