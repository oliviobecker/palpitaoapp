using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Ocr;

public partial class PredictionImportService : IPredictionImportService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public PredictionImportService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

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
    // e.g. "Bélgica 🇧🇪 2 x 1 🇪🇬 Egito").
    [GeneratedRegex(@"[\p{So}\p{Sk}\p{Cs}\p{Cf}\p{Co}\uFE00-\uFE0F]")]
    private static partial Regex SymbolNoise();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    // Header with the participant's name, e.g. "Gilberto, Rodada 2 (1a fase de grupos)".
    [GeneratedRegex(@"^(.+?),\s*rodada\b", RegexOptions.IgnoreCase)]
    private static partial Regex ParticipantHeader();

    private static readonly Dictionary<string, string> TeamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["man city"] = "manchester city",
        ["mancity"] = "manchester city",
        ["man united"] = "manchester united",
        ["man utd"] = "manchester united",
        ["man u"] = "manchester united",
        ["spurs"] = "tottenham",
        ["united"] = "manchester united",
        ["city"] = "manchester city",
    };

    public IReadOnlyList<ParsedPrediction> Parse(string text)
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

            // Otherwise it may be a participant name. A "Nome, Rodada N (...)"
            // header is a strong signal; a plain clean name overrides too. Garbled
            // OCR lines (symbols, digits, ALL-CAPS noise) are ignored so they do
            // not steal the participant context from the real matches.
            var header = ParticipantHeader().Match(line);
            if (header.Success)
            {
                currentName = header.Groups[1].Value.Trim();
            }
            else if (LooksLikeName(line))
            {
                currentName = line;
            }
        }

        return result;
    }

    /// <summary>
    /// True for plain participant names ("João", "Pedro Silva"): letters and
    /// spaces only, at least one lowercase letter, up to four words. Filters out
    /// titles, timestamps and garbled OCR lines.
    /// </summary>
    private static bool LooksLikeName(string line)
    {
        if (line.Length is < 2 or > 40)
        {
            return false;
        }

        var hasLower = false;
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

            hasLower |= char.IsLower(c);
        }

        return hasLower && words <= 4;
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
        return WhitespaceRun().Replace(withoutSymbols, " ").Trim();
    }

    public IReadOnlyList<OcrPredictionCandidate> BuildCandidates(
        Guid batchId,
        Guid roundId,
        string text,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyList<User> participants)
    {
        var now = DateTime.UtcNow;
        var candidates = new List<OcrPredictionCandidate>();

        foreach (var parsed in Parse(text))
        {
            var userId = ResolveParticipant(parsed.ParticipantName, participants);
            var roundMatchId = ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, matches);

            var confidence = (userId is not null ? 0.5 : 0.0) + (roundMatchId is not null ? 0.5 : 0.0);
            var needsReview = userId is null || roundMatchId is null;

            candidates.Add(new OcrPredictionCandidate
            {
                Id = Guid.NewGuid(),
                OcrImportBatchId = batchId,
                RoundId = roundId,
                UserId = userId,
                ParticipantNameRaw = parsed.ParticipantName,
                RoundMatchId = roundMatchId,
                MatchTextRaw = parsed.MatchText,
                PredictedHomeScore = parsed.HomeScore,
                PredictedAwayScore = parsed.AwayScore,
                Confidence = confidence,
                NeedsReview = needsReview,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        return candidates;
    }

    public async Task ConfirmAsync(Guid batchId, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var batch = await _db.OcrImportBatches
            .Include(b => b.Candidates)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.Round!.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        var incomplete = batch.Candidates.Any(c =>
            c.UserId is null || c.RoundMatchId is null ||
            c.PredictedHomeScore is null || c.PredictedAwayScore is null ||
            c.PredictedHomeScore < 0 || c.PredictedAwayScore < 0);

        if (batch.Candidates.Count == 0 || incomplete)
        {
            throw new BusinessRuleException("ocr.incompleteCandidates");
        }

        var now = DateTime.UtcNow;
        foreach (var c in batch.Candidates)
        {
            var existing = await _db.Predictions
                .FirstOrDefaultAsync(p => p.RoundMatchId == c.RoundMatchId && p.UserId == c.UserId, ct);

            if (existing is null)
            {
                _db.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    RoundId = batch.RoundId,
                    RoundMatchId = c.RoundMatchId!.Value,
                    UserId = c.UserId!.Value,
                    PredictedHomeScore = c.PredictedHomeScore!.Value,
                    PredictedAwayScore = c.PredictedAwayScore!.Value,
                    SubmittedAt = now,
                    Source = PredictionSource.AdminOcr,
                    CreatedByUserId = adminId,
                });
            }
            else
            {
                existing.PredictedHomeScore = c.PredictedHomeScore!.Value;
                existing.PredictedAwayScore = c.PredictedAwayScore!.Value;
                existing.UpdatedAt = now;
                existing.Source = PredictionSource.AdminOcr;
                existing.UpdatedByUserId = adminId;
            }
        }

        batch.Status = OcrBatchStatus.Confirmed;
        batch.ConfirmedAt = now;

        _audit.Add(adminId, "OcrImportConfirmed", nameof(OcrImportBatch), batch.Id.ToString(),
            new { batch.RoundId, count = batch.Candidates.Count });

        await _db.SaveChangesAsync(ct);
    }

    // -----------------------------------------------------------------------
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
        var a = ScorePair().Match(text);
        if (a.Success
            && ParseScore(a.Groups[1].Value) is { } homeA
            && ParseScore(a.Groups[2].Value) is { } awayA)
        {
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

    private static Guid? ResolveParticipant(string? name, IReadOnlyList<User> participants)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var exact = participants.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Id;
        }

        var contains = participants
            .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                || name.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return contains.Count == 1 ? contains[0].Id : null;
    }

    private static Guid? ResolveMatch(string homeRaw, string awayRaw, IReadOnlyList<RoundMatch> matches)
    {
        var hits = matches
            .Where(m => TeamMatches(homeRaw, m.HomeTeam?.Name) && TeamMatches(awayRaw, m.AwayTeam?.Name))
            .ToList();

        return hits.Count == 1 ? hits[0].Id : null;
    }

    private static bool TeamMatches(string raw, string? teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return false;
        }

        var r = Normalize(raw);
        var t = teamName.Trim().ToLowerInvariant();
        return r == t || t.Contains(r) || r.Contains(t);
    }

    private static string Normalize(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        return TeamAliases.TryGetValue(s, out var alias) ? alias : s;
    }
}
