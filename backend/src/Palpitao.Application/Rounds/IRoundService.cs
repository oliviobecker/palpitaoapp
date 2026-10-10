using Palpitao.Domain.Rounds;

namespace Palpitao.Application.Rounds;

public interface IRoundService
{
    Task<IReadOnlyList<RoundSummaryDto>> GetAllAsync(CancellationToken ct);
    Task<RoundDto> GetByIdAsync(Guid roundId, CancellationToken ct);

    Task<RoundDto> CreateAsync(CreateRoundRequest request, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> UpdateAsync(Guid roundId, UpdateRoundRequest request, Guid actingUserId, CancellationToken ct);

    Task<RoundDto> PublishAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> LockAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> CancelAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> ReopenAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> UnlockAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    /// <summary>Undoes a cancellation: the round goes back to the status it was cancelled from.</summary>
    Task<RoundDto> RestoreAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    Task<MatchDto> AddMatchAsync(Guid roundId, CreateMatchRequest request, Guid actingUserId, CancellationToken ct);
    Task<MatchDto> UpdateMatchAsync(Guid matchId, UpdateMatchRequest request, Guid actingUserId, CancellationToken ct);
    Task DeleteMatchAsync(Guid matchId, string? overrideLockJustification, Guid actingUserId, CancellationToken ct);
}
