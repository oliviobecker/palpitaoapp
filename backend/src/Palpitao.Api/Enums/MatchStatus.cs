namespace Palpitao.Api.Enums;

/// <summary>
/// Live status of a match within a round. Explicit non-zero values so EF's
/// "store default" sentinel (CLR default 0) does not override an explicit
/// <see cref="NotStarted"/> on insert.
/// </summary>
public enum MatchStatus
{
    NotStarted = 1,
    InProgress = 2,
    Finished = 3,
    Postponed = 4,
    Cancelled = 5,
}
