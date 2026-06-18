using Palpitao.Api.DTOs.Scoring;

namespace Palpitao.Api.Services.Standings;

public interface IStandingsService
{
    /// <summary>
    /// Rebuilds the season standing from the stored per-round results. Ordering:
    /// total points desc, fewer absences, then name ascending.
    /// </summary>
    Task RecomputeSeasonStandingsAsync(Guid seasonId, CancellationToken ct);

    Task<IReadOnlyList<StandingDto>> GetStandingsAsync(Guid seasonId, CancellationToken ct);
}
