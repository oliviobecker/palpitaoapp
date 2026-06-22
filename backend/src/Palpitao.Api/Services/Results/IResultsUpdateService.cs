using Palpitao.Api.DTOs.Results;

namespace Palpitao.Api.Services.Results;

public interface IResultsUpdateService
{
    /// <summary>
    /// Refreshes the round's match results from the configured provider (when
    /// enabled) and stamps the round for the temporary standings. Never moves the
    /// round to <c>Scored</c> — that stays with the official scoring flow.
    /// </summary>
    Task<RefreshResultsResponse> RefreshAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    /// <summary>
    /// Background-safe refresh of all in-play rounds (Published/Locked) across every
    /// group. Not group-scoped; never closes a round. Returns updated match count.
    /// </summary>
    Task<int> RefreshAllActiveRoundsAsync(CancellationToken ct);
}
