namespace Palpitao.Api.Common;

/// <summary>
/// The general prediction deadline of a round: predictions close one minute before the
/// first match kicks off. Single source of the lead time — every gate (participant save,
/// admin manual entry, mirror release) derives from here rather than comparing against
/// <c>FirstMatchStartsAt</c> directly.
/// </summary>
public static class PredictionDeadline
{
    /// <summary>Minutes before the first kickoff at which predictions close.</summary>
    public const int LeadMinutes = 1;

    /// <summary>
    /// The deadline for a round whose first match starts at
    /// <paramref name="firstMatchStartsAt"/>, or null while the round has no matches /
    /// is not published yet.
    /// </summary>
    public static DateTime? From(DateTime? firstMatchStartsAt)
        => firstMatchStartsAt?.AddMinutes(-LeadMinutes);
}
