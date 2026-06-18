using Palpitao.Api.DTOs.Predictions;

namespace Palpitao.Api.Services.Predictions;

public interface IPredictionsService
{
    Task<MyPredictionsDto> GetMyPredictionsAsync(Guid roundId, Guid userId, CancellationToken ct);

    Task<MyPredictionsDto> SavePredictionsAsync(
        Guid roundId, Guid userId, SavePredictionsRequest request, bool isEdit, CancellationToken ct);

    Task<MirrorDto> GetMirrorAsync(Guid roundId, Guid requestingUserId, CancellationToken ct);
}
