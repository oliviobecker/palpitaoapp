using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Resolves the raw names produced by <see cref="OcrTextParser"/> to actual
/// participants and round matches. Fuzzy but conservative: only returns a result
/// when the match is unambiguous, otherwise null (flagged for manual review).
/// </summary>
public static class OcrTeamMatcher
{
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

        // Short names emitted by the copy-ready WhatsApp messages (frontend
        // shared/utils/team-name.util.ts). Only the ones that are not a substring of
        // the seeded Team.Name need a row here — "Hull", "West Brom", "Sheffield Wed"
        // and the rest already resolve through the containment check in TeamMatches.
        // "sheffield utd" also disambiguates the two Sheffields: a bare "Sheffield"
        // hits both fixtures, so ResolveMatch would flag it for review.
        ["wolves"] = "wolverhampton wanderers",
        ["qpr"] = "queens park rangers",
        ["sheffield utd"] = "sheffield united",
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

        var t = Canonical(teamName);
        var r = Canonical(raw);

        // The raw form is tried before the alias: a Team may itself carry the short
        // name — fixture feeds ship "Wolves", "Man Utd", "Spurs" and FixtureImportService
        // creates the row verbatim — and rewriting it to the canonical name would then
        // stop it matching itself.
        return Overlaps(r, t) || (TeamAliases.TryGetValue(r, out var alias) && Overlaps(alias, t));

        static bool Overlaps(string a, string b) => a == b || b.Contains(a) || a.Contains(b);
    }

    /// <summary>
    /// Lowercases and drops what OCR cannot give back: OcrTextParser.CleanTeam keeps
    /// letters only, so "Brighton &amp; Hove Albion" is read as "Brighton Hove Albion".
    /// Comparing both sides without the ampersand keeps those clubs matchable.
    /// </summary>
    private static string Canonical(string value) => string.Join(
        ' ',
        value.ToLowerInvariant().Replace('&', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
