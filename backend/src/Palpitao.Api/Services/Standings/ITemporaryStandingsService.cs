using Palpitao.Api.DTOs.Results;

namespace Palpitao.Api.Services.Standings;

public interface ITemporaryStandingsService
{
    /// <summary>
    /// Computes the round's <b>temporary</b> standings on demand from the results
    /// currently available (in-progress or finished matches with a score), using the
    /// same <c>ScoringService</c>. Lists the whole active roster, so a participant who
    /// has not predicted shows up on zero. Still never <i>applies</i> absence penalties,
    /// eliminations or the Flávio rule, and never touches the official season standings —
    /// but once predictions have closed it <i>flags</i> who will be recorded absent when
    /// the round is scored (<c>WillBeAbsent</c>), without zeroing anyone's points.
    /// </summary>
    Task<TemporaryStandingsDto> GetTemporaryStandingsAsync(Guid roundId, CancellationToken ct);
}
