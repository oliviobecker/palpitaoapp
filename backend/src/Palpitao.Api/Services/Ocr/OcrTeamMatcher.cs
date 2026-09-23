using System.Globalization;
using System.Text;
using Palpitao.Api.Common;
using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Resolves the raw names produced by <see cref="OcrTextParser"/> to actual
/// participants and round matches. Fuzzy but conservative: only returns a result
/// when the match is unambiguous, otherwise null (flagged for manual review).
/// </summary>
public static class OcrTeamMatcher
{
    /// <summary>
    /// Loose aliases that only make sense in this fuzzy, human-reviewed flow. The real
    /// spelling map lives in <see cref="FootballReference.Canonical"/> — shared with the
    /// fixture import, and where the short names the group message prints ("Wolves",
    /// "QPR", "MK Dons", "Sheffield Utd") are registered. Only entries too loose for
    /// that map belong here: a bare "city"/"united" would wreck import (Bristol City,
    /// Leeds United, …) but is worth guessing at when an admin reviews the result.
    /// </summary>
    /// <summary>Shortest name that may absorb a wrong character; below it only accents are folded.</summary>
    private const int MinimumLengthForAnEdit = 5;

    private static readonly Dictionary<string, string> TeamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mancity"] = "manchester city",
        ["man u"] = "manchester united",
        ["united"] = "manchester united",
        ["city"] = "manchester city",
    };

    /// <summary>
    /// Resolves a parsed name to a participant: a previously confirmed alias, then an exact
    /// match, then a unique substring match, then a one-edit fuzzy match.
    /// </summary>
    /// <param name="aliases">
    /// Names an admin has already confirmed for this group, keyed by <see cref="NormalizeAlias"/>.
    /// Checked first and trusted outright: it is a human decision, not a guess, so it beats a
    /// substring match that would otherwise pick a different member with a similar name.
    /// </param>
    public static Guid? ResolveParticipant(
        string? name,
        IReadOnlyList<User> participants,
        IReadOnlyDictionary<string, Guid>? aliases = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (aliases is not null
            && aliases.TryGetValue(NormalizeAlias(name), out var aliased)
            // Only while that participant is still on the roster handed in — a member who left
            // the group must not keep absorbing rows.
            && participants.Any(p => p.Id == aliased))
        {
            return aliased;
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

        if (contains.Count == 1)
        {
            return contains[0].Id;
        }

        // Only when nothing matched at all — a name OCR mangled by a letter or a dropped
        // accent ("Joao" for "João"). Never when the strict pass already found candidates:
        // that is real ambiguity and belongs in manual review.
        if (contains.Count > 0)
        {
            return null;
        }

        var fuzzy = participants.Where(p => Fuzzy(name, p.Name)).ToList();
        return fuzzy.Count == 1 ? fuzzy[0].Id : null;
    }

    /// <summary>How a line was tied to a fixture, and whether it took the approximate tier.</summary>
    /// <param name="Approximate">
    /// Resolved by <see cref="ResolveByAnchor"/>: one side was too garbled for the one-edit budget.
    /// Right on every garbled line measured, but still a guess, so the import sends it to review.
    /// </param>
    public readonly record struct MatchResolution(Guid? MatchId, bool Approximate);

    /// <summary>Resolves raw home/away names to a round match, only when exactly one fits.</summary>
    /// <param name="catalogue">
    /// Every club name. Only with it does the approximate tier run: it is what lets that tier refuse
    /// a "garbled" side that is really another club.
    /// </param>
    public static Guid? ResolveMatch(
        string homeRaw,
        string awayRaw,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyCollection<string>? catalogue = null) =>
        Resolve(homeRaw, awayRaw, matches, catalogue).MatchId;

    /// <inheritdoc cref="ResolveMatch"/>
    public static MatchResolution Resolve(
        string homeRaw,
        string awayRaw,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyCollection<string>? catalogue = null)
    {
        var hits = matches
            .Where(m => TeamMatches(homeRaw, m.HomeTeam?.Name) && TeamMatches(awayRaw, m.AwayTeam?.Name))
            .ToList();

        if (hits.Count == 1)
        {
            return new MatchResolution(hits[0].Id, false);
        }

        // A strict hit is never second-guessed: two strict hits mean the line genuinely fits two
        // fixtures ("Sheffield" with both Sheffields on the card), which is exactly what manual
        // review is for. The fuzzy tier only rescues lines nothing matched — a single dropped
        // letter ("Coventy") otherwise costs the admin the whole row.
        if (hits.Count > 0)
        {
            return default;
        }

        var fuzzyHits = matches
            .Where(m => TeamMatches(homeRaw, m.HomeTeam?.Name, fuzzy: true)
                && TeamMatches(awayRaw, m.AwayTeam?.Name, fuzzy: true))
            .ToList();

        if (fuzzyHits.Count > 0)
        {
            return fuzzyHits.Count == 1 ? new MatchResolution(fuzzyHits[0].Id, false) : default;
        }

        if (catalogue is null)
        {
            return default;
        }

        var anchored = ResolveByAnchor(homeRaw, awayRaw, matches, catalogue);
        return anchored is null ? default : new MatchResolution(anchored, Approximate: true);
    }

    /// <summary>
    /// Last tier, for the low-resolution screenshots (WhatsApp Desktop, ~9px text) where OCR gets
    /// one side of a line badly wrong: "Torrenham 2 x 1 Aston Villa", "Newcastle 3 x 1 Hll",
    /// "Lmon 1 x 0 Bradford". One side still reads cleanly, and a club plays once per round, so that
    /// side alone pins the fixture; the other side then only has to be plausible for the club it
    /// sits against, not within the one-edit budget that has to keep the whole catalogue apart.
    ///
    /// Three conditions, all required. The clean side pins exactly one fixture at its own position
    /// (home against home, away against away). The garbled side is plausible for that fixture's club
    /// only. And the garbled side is not itself a club of the catalogue: "Brentford 1x1 Nottingham"
    /// from another round's list, against "Brentford x Tottenham" on this card, is a real club
    /// that happens to look like the one expected — not a misreading of it.
    /// <c>OcrShortNameRoundTripTests</c> sweeps every pair of clubs to hold that line.
    /// </summary>
    private static Guid? ResolveByAnchor(
        string homeRaw, string awayRaw, IReadOnlyList<RoundMatch> matches, IReadOnlyCollection<string> catalogue)
    {
        var pinnedByHome = matches.Where(m => TeamMatches(homeRaw, m.HomeTeam?.Name, fuzzy: true)).ToList();
        var pinnedByAway = matches.Where(m => TeamMatches(awayRaw, m.AwayTeam?.Name, fuzzy: true)).ToList();
        var home = pinnedByHome.Count == 1 ? pinnedByHome[0] : null;
        var away = pinnedByAway.Count == 1 ? pinnedByAway[0] : null;

        // Both sides pinning means they pinned different fixtures (the same one would have matched
        // above): a line that mixes two fixtures is not something to guess at.
        if (home is not null && away is not null)
        {
            return null;
        }

        if (home is not null)
        {
            return IsMisreadingOf(awayRaw, home.AwayTeam?.Name, catalogue) ? home.Id : null;
        }

        if (away is not null)
        {
            return IsMisreadingOf(homeRaw, away.HomeTeam?.Name, catalogue) ? away.Id : null;
        }

        return null;
    }

    /// <summary>Largest share of a name OCR may get wrong and still count as plausible for a club.</summary>
    private const double PlausibleEditShare = 0.4;

    /// <summary>
    /// Words too common among club names to identify one: measured against them, "Coventry" looks
    /// like any "… County" and "Hull" like any "… City". A club's distinctive words and its short
    /// names still count.
    /// </summary>
    private static readonly HashSet<string> GenericClubWords = new(StringComparer.Ordinal)
    {
        "afc", "fc", "city", "town", "united", "county", "rovers", "wanderers", "athletic", "albion",
        "rangers", "north", "end", "park", "hove", "and",
    };

    /// <summary>
    /// True when <paramref name="raw"/> reads as a garbled <paramref name="teamName"/>: within
    /// <see cref="PlausibleEditShare"/> of some form of the club — its distinctive words ("hull" for
    /// Hull City) or the short names the message prints ("man utd"), with and without the rn/m
    /// ligature — and not a clean reading of a different club.
    /// </summary>
    private static bool IsMisreadingOf(string raw, string? teamName, IReadOnlyCollection<string> catalogue)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return false;
        }

        var r = Fold(raw.Replace('&', ' '));
        if (r.Length == 0)
        {
            return false;
        }

        var plausible = NameForms(teamName).Any(form =>
        {
            var budget = (int)Math.Floor(PlausibleEditShare * Math.Max(r.Length, form.Length));
            return WithinDistance(r, form, budget) || WithinDistance(Ligature(r), Ligature(form), budget);
        });

        return plausible && !catalogue.Any(other => !IsSameClub(other, teamName) && TeamMatches(raw, other, fuzzy: true));
    }

    private static bool IsSameClub(string a, string b) =>
        Comparable(a) == Comparable(b)
        || FootballReference.Canonical(a) == FootballReference.Canonical(b);

    /// <summary>
    /// The club's folded name, every run of its words that is not only generic words, and the
    /// short names that point at it.
    /// </summary>
    private static IEnumerable<string> NameForms(string teamName)
    {
        var full = Fold(teamName.Replace('&', ' '));
        var words = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var start = 0; start < words.Length; start++)
        {
            for (var count = 1; start + count <= words.Length; count++)
            {
                if (words.Skip(start).Take(count).All(GenericClubWords.Contains))
                {
                    continue;
                }

                yield return string.Join(' ', words, start, count);
            }
        }

        var canonical = FootballReference.Canonical(teamName);
        foreach (var (shortName, fullName) in FootballReference.Aliases)
        {
            if (string.Equals(fullName, canonical, StringComparison.OrdinalIgnoreCase))
            {
                yield return Fold(shortName);
            }
        }
    }

    /// <summary>
    /// Resolves the participant a screenshot's file name points at (see
    /// <see cref="OcrTextParser.NameFromFileName"/>). Stricter than <see cref="ResolveParticipant"/>
    /// on purpose — the file name is trusted over what OCR reads, so it must not guess: a learned
    /// alias, the same name, whole words of it ("Valter" for "Valter Silva", "De Farias" for "Felipe
    /// de Farias"), or one wrong letter in a longer word ("Vilacao"). Never a bare substring, which
    /// would let a two-letter participant such as "PL" claim "Complete.png". The first word is tried
    /// alone only when the whole name found nobody at all ("Ezau Unica"), never when it found two.
    /// </summary>
    public static Guid? ResolveParticipantFromFileName(
        string? stem,
        IReadOnlyList<User> participants,
        IReadOnlyDictionary<string, Guid>? aliases = null)
    {
        if (string.IsNullOrWhiteSpace(stem))
        {
            return null;
        }

        var candidates = FileNameCandidates(stem, participants, aliases);
        if (candidates.Count == 0 && stem.Contains(' '))
        {
            candidates = FileNameCandidates(stem[..stem.IndexOf(' ')], participants, aliases);
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static List<Guid> FileNameCandidates(
        string name, IReadOnlyList<User> participants, IReadOnlyDictionary<string, Guid>? aliases)
    {
        if (aliases is not null
            && aliases.TryGetValue(NormalizeAlias(name), out var aliased)
            && participants.Any(p => p.Id == aliased))
        {
            return [aliased];
        }

        var key = Fold(name);
        var exact = participants.Where(p => Fold(p.Name) == key).Select(p => p.Id).ToList();
        if (exact.Count > 0)
        {
            return exact;
        }

        var words = key.Split(' ');
        var byWords = participants
            .Where(p =>
            {
                var own = Fold(p.Name).Split(' ');
                return words.All(own.Contains) || own.All(words.Contains);
            })
            .Select(p => p.Id)
            .ToList();
        if (byWords.Count > 0)
        {
            return byWords;
        }

        return participants
            .Where(p => words.Any(w => w.Length >= MinimumLengthForAnEdit
                && Fold(p.Name).Split(' ').Any(own => WithinDistance(w, own, 1)))
                || Fuzzy(key, p.Name))
            .Select(p => p.Id)
            .ToList();
    }

    private static bool TeamMatches(string raw, string? teamName, bool fuzzy = false)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return false;
        }

        var t = Comparable(teamName);
        var r = Comparable(raw);

        // The raw form is tried before any rewrite: a Team may itself carry the short
        // name — fixture feeds ship "Wolves", "Man Utd", "Spurs" and FixtureImportService
        // creates the row verbatim — and rewriting it to the canonical name would then
        // stop it matching itself.
        return Overlaps(r, t)
            || Overlaps(Comparable(FootballReference.Canonical(raw)), t)
            || (TeamAliases.TryGetValue(r, out var alias) && Overlaps(alias, t))
            || (fuzzy && MatchesThroughShortName(r, t));

        bool Overlaps(string a, string b) =>
            a == b || b.Contains(a) || a.Contains(b) || (fuzzy && Fuzzy(a, b));
    }

    /// <summary>
    /// Last resort: the raw name is one edit from a short name the group message prints, whose
    /// club is the one on the card. Canonical() is an exact-key lookup, so "Weolves" never
    /// becomes "wolverhampton wanderers" — and comparing "weolves" to the full name directly is
    /// far past a one-edit budget. Going through the alias key bridges the two.
    /// </summary>
    private static bool MatchesThroughShortName(string raw, string teamName)
    {
        foreach (var (shortName, fullName) in FootballReference.Aliases)
        {
            if (!Fuzzy(raw, shortName))
            {
                continue;
            }

            var canonical = Comparable(fullName);
            if (canonical == teamName || teamName.Contains(canonical) || canonical.Contains(teamName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lowercases and drops what OCR cannot give back: OcrTextParser.CleanTeam keeps
    /// letters only, so "Brighton &amp; Hove Albion" is read as "Brighton Hove Albion".
    /// Comparing both sides without the ampersand keeps those clubs matchable.
    /// </summary>
    private static string Comparable(string value) => string.Join(
        ' ',
        value.ToLowerInvariant().Replace('&', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// True when two names are within a small edit distance — the tolerance for characters
    /// OCR simply got wrong ("Coventy" for "Coventry", "Joao" for "João").
    ///
    /// At most one edit, and none at all under 5 characters. Longer names do not earn a
    /// bigger budget even though they offer OCR more to get wrong: English club names cluster
    /// too tightly for it. "Northampton" and "Southampton" are two edits apart, as are
    /// Luton/Leyton and Barnsley/Burnley — a budget of two silently merges the first pair.
    ///
    /// The shorter side is also compared against every run of consecutive words of the longer
    /// one, because the message prints short names while the catalogue stores full ones:
    /// "coventy" is one edit from "coventry", the first word of "coventry city".
    ///
    /// <see cref="OcrShortNameRoundTripTests"/> sweeps the whole seeded catalogue pairwise to
    /// prove no two clubs collide under these budgets. Tighten the budgets if it ever fails —
    /// an allowlist would only hide the next collision.
    /// </summary>
    private static bool Fuzzy(string a, string b)
    {
        var x = Fold(a);
        var y = Fold(b);

        // Both spellings are tried: the ligature closes "Blackbum" (an rn read as m) but opens a
        // gap on the reverse mistake — "Blackburmn", where OCR added the m, is one edit from the
        // club as written and two once every m has become rn.
        return FuzzyFolded(Ligature(x), Ligature(y)) || FuzzyFolded(x, y);
    }

    private static bool FuzzyFolded(string x, string y)
    {
        if (x.Length == 0 || y.Length == 0)
        {
            return false;
        }

        var (shorter, longer) = x.Length <= y.Length ? (x, y) : (y, x);
        // Below the floor the budget drops to zero rather than bailing out: at zero this is
        // pure accent folding ("Joao" for "João"), which is a normalisation and not a guess,
        // and short names must still get that.
        var budget = shorter.Length < MinimumLengthForAnEdit ? 0 : 1;

        if (WithinDistance(shorter, longer, budget))
        {
            return true;
        }

        var words = longer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var start = 0; start < words.Length; start++)
        {
            for (var count = 1; start + count <= words.Length; count++)
            {
                var span = string.Join(' ', words, start, count);
                if (span.Length != longer.Length && WithinDistance(shorter, span, budget))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Lookup key for a learned participant alias: lowercased, accent-folded and with runs of
    /// whitespace collapsed, so "Paraguaio", "paraguaio" and "PARAGUAIO " are one entry. Used on
    /// both sides — writing the alias and reading it back — so the two can never drift.
    /// </summary>
    public static string NormalizeAlias(string value) => Fold(value);

    /// <summary>
    /// Collapses the one confusion Tesseract makes constantly on these names: "rn" and "m" are
    /// the same handful of pixels at screenshot resolution, so it returns "Blackbum" for
    /// Blackburn, "Bumley" for Burnley and "Boumesmouth" for Bournemouth. Each of those is two
    /// edits from its club — delete the r, swap the n — which is exactly one past the budget, and
    /// widening the budget instead would merge Barnsley into Burnley. Rewriting every m as rn on
    /// both sides makes the pair identical without giving anything else more room: it is a
    /// normalisation, like accent folding, not a guess.
    ///
    /// Only for comparison. <see cref="NormalizeAlias"/> deliberately does not do this — it is the
    /// stored key of a learned alias, and rewriting it would orphan every alias already in a group.
    /// </summary>
    private static string Ligature(string value) => value.Replace("m", "rn", StringComparison.Ordinal);

    /// <summary>Lowercases and strips accents, so "Joao" and "João" compare equal.</summary>
    internal static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return string.Join(
            ' ',
            builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Levenshtein distance, abandoned as soon as every path exceeds the budget.</summary>
    internal static bool WithinDistance(string a, string b, int budget)
    {
        if (Math.Abs(a.Length - b.Length) > budget)
        {
            return false;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowBest = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowBest = Math.Min(rowBest, current[j]);
            }

            if (rowBest > budget)
            {
                return false;
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length] <= budget;
    }
}
