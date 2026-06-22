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
