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
    private static readonly Dictionary<string, string> TeamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mancity"] = "manchester city",
        ["man u"] = "manchester united",
        ["united"] = "manchester united",
        ["city"] = "manchester city",
    };

    /// <summary>Resolves a parsed name to a participant: exact match, else a unique substring match.</summary>
    public static Guid? ResolveParticipant(string? name, IReadOnlyList<User> participants)
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

    /// <summary>Resolves raw home/away names to a round match, only when exactly one fits.</summary>
    public static Guid? ResolveMatch(string homeRaw, string awayRaw, IReadOnlyList<RoundMatch> matches)
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

        var t = Comparable(teamName);
        var r = Comparable(raw);

        // The raw form is tried before any rewrite: a Team may itself carry the short
        // name — fixture feeds ship "Wolves", "Man Utd", "Spurs" and FixtureImportService
        // creates the row verbatim — and rewriting it to the canonical name would then
        // stop it matching itself.
        return Overlaps(r, t)
            || Overlaps(Comparable(FootballReference.Canonical(raw)), t)
            || (TeamAliases.TryGetValue(r, out var alias) && Overlaps(alias, t));

        static bool Overlaps(string a, string b) => a == b || b.Contains(a) || a.Contains(b);
    }

    /// <summary>
    /// Lowercases and drops what OCR cannot give back: OcrTextParser.CleanTeam keeps
    /// letters only, so "Brighton &amp; Hove Albion" is read as "Brighton Hove Albion".
    /// Comparing both sides without the ampersand keeps those clubs matchable.
    /// </summary>
    private static string Comparable(string value) => string.Join(
        ' ',
        value.ToLowerInvariant().Replace('&', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
