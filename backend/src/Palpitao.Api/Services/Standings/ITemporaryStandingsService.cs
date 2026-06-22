using Palpitao.Api.DTOs.Results;

namespace Palpitao.Api.Services.Standings;

public interface ITemporaryStandingsService
{
    /// <summary>
    /// Computes the round's <b>temporary</b> standings on demand from the results
    /// currently available (in-progress or finished matches with a score), using the
    /// same <c>ScoringService</c>. Never applies absences, eliminations or the Flávio
    /// rule, and never touches the official season standings.
    /// </summary>
    Task<TemporaryStandingsDto> GetTemporaryStandingsAsync(Guid roundId, CancellationToken ct);
}
