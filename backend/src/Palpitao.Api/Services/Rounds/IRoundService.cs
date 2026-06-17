using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Rounds;

namespace Palpitao.Api.Services.Rounds;

public interface IRoundService
{
    Task<IReadOnlyList<RoundSummaryDto>> GetAllAsync(CancellationToken ct);
    Task<RoundDto> GetByIdAsync(Guid roundId, CancellationToken ct);

    Task<RoundDto> CreateAsync(CreateRoundRequest request, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> UpdateAsync(Guid roundId, UpdateRoundRequest request, Guid actingUserId, CancellationToken ct);

    Task<RoundDto> PublishAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> LockAsync(Guid roundId, Guid actingUserId, CancellationToken ct);
    Task<RoundDto> CancelAsync(Guid roundId, Guid actingUserId, CancellationToken ct);

    Task<MatchDto> AddMatchAsync(Guid roundId, CreateMatchRequest request, Guid actingUserId, CancellationToken ct);
    Task<MatchDto> UpdateMatchAsync(Guid matchId, UpdateMatchRequest request, Guid actingUserId, CancellationToken ct);
    Task DeleteMatchAsync(Guid matchId, string? overrideLockJustification, Guid actingUserId, CancellationToken ct);
}
