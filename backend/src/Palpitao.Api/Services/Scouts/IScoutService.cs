using Palpitao.Api.DTOs.Scouts;

namespace Palpitao.Api.Services.Scouts;

public interface IScoutService
{
    /// <summary>
    /// Builds the scout of a round: each match with its participants grouped by the
    /// exact scoreline they predicted.
    /// </summary>
    Task<RoundScoutDto> GetRoundScoutAsync(Guid roundId, CancellationToken ct);
}
