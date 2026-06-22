using Palpitao.Api.DTOs.Seasons;

namespace Palpitao.Api.Services.Seasons;

public interface ISeasonService
{
    Task<IReadOnlyList<SeasonDto>> ListAsync(CancellationToken ct);
    Task<SeasonDto?> GetActiveAsync(CancellationToken ct);
    Task<SeasonDto> CreateAsync(SeasonRequest request, Guid actingUserId, CancellationToken ct);
    Task<SeasonDto> UpdateAsync(Guid id, SeasonRequest request, Guid actingUserId, CancellationToken ct);
    Task<SeasonDto> SetActiveAsync(Guid id, Guid actingUserId, CancellationToken ct);
}
