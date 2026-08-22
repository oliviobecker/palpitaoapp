using System.Text;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Maps the status label an external results feed puts on a match onto <see cref="MatchStatus"/>.
/// Shared by every <see cref="IResultsProvider"/> so a spelling only has to be taught once.
/// <para>
/// Labels are normalised down to letters and digits before matching, so <c>SECOND_HALF</c>,
/// <c>Second Half</c> and <c>secondHalf</c> are the same key. What is left after that is a small
/// vocabulary plus three fallbacks for a label we have never seen: a running clock means the match
/// is being played, a full scoreline means it is over, and anything else means it has not started.
/// </para>
/// </summary>
public static class MatchStatusParser
{
    public static MatchStatus Parse(string? raw, int? homeScore, int? awayScore, string? liveClock = null)
        => Parse(raw, homeScore, awayScore, liveClock, out _);

    /// <param name="unknownLabel">
    /// True when the feed sent a label this table does not know — worth logging once, so the next
    /// rename upstream shows up in the logs instead of silently becoming "not started".
    /// </param>
    public static MatchStatus Parse(
        string? raw, int? homeScore, int? awayScore, string? liveClock, out bool unknownLabel)
    {
        var key = Normalize(raw);
        if (TryMap(key, out var status))
        {
            unknownLabel = false;
            return status;
        }

        unknownLabel = key.Length > 0;

        // A live match has a scoreline too, so the running clock has to be read before the
        // "it has a score, therefore it is over" rule below.
        if (IsLiveClock(liveClock))
        {
            return MatchStatus.InProgress;
        }

        return homeScore is not null && awayScore is not null
            ? MatchStatus.Finished
            : MatchStatus.NotStarted;
    }

    private static bool TryMap(string key, out MatchStatus status)
    {
        // FULL_TIME, FULL_TIME_PENALTIES, FULL_TIME_EXTRA_TIME… the suffixes keep growing.
        if (key.StartsWith("fulltime", StringComparison.Ordinal))
        {
            status = MatchStatus.Finished;
            return true;
        }

        // EXTRA_TIME_FIRST_HALF, EXTRA_TIME_HALF_TIME… all mean the match is still being played.
        if (key.StartsWith("extratime", StringComparison.Ordinal))
        {
            status = MatchStatus.InProgress;
            return true;
        }

        status = key switch
        {
            "live" or "inprogress" or "playing" or "1h" or "2h" or "ht" or "et"
                or "firsthalf" or "secondhalf" or "halftime"
                or "penalties" or "penaltyshootout" or "paused" or "break" => MatchStatus.InProgress,

            // "aet"/"pen" are the short codes other feeds use for a match *decided* after extra
            // time or on penalties — unlike "penaltyshootout" above, which is still being taken.
            "ft" or "finished" or "ended" or "final" or "postmatch" or "matchfinished"
                or "aet" or "afterextratime" or "pen" or "afterpenalties" => MatchStatus.Finished,

            "prematch" or "notstarted" or "scheduled" or "upcoming" or "ns" or "tbd" => MatchStatus.NotStarted,
            "postponed" or "susp" or "suspended" or "delayed" => MatchStatus.Postponed,
            "cancelled" or "canceled" or "abandoned" or "awarded" or "walkover" => MatchStatus.Cancelled,

            // MatchStatus starts at 1, so the CLR default doubles as "no match".
            _ => default,
        };

        return status != default;
    }

    /// <summary>
    /// OneFootball puts the running clock in <c>timePeriod</c> ("66'", "45+2'"); a played card
    /// carries "Full time" and an upcoming one carries nothing. Requiring a digit *and* a minute
    /// marker keeps an unknown label from being read as live off "Full time" — or off a "20:00"
    /// kickoff, should the feed ever put one there.
    /// </summary>
    private static bool IsLiveClock(string? clock)
        => !string.IsNullOrWhiteSpace(clock)
            && clock.Any(char.IsDigit)
            && (clock.Contains('\'') || clock.Contains('+'));

    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer.Append(char.ToLowerInvariant(c));
            }
        }

        return buffer.ToString();
    }
}
