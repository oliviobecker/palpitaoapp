using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.AdminPredictions;

public interface IAdminPredictionService
{
    /// <summary>Registers/edits a participant's predictions on behalf of them (admin).</summary>
    Task SaveManualAsync(Guid roundId, ManualPredictionRequest request, Guid adminId, CancellationToken ct);

    /// <summary>A participant's current predictions for a round, to preload the manual screen.</summary>
    Task<AdminParticipantPredictionsDto> GetParticipantPredictionsAsync(Guid roundId, Guid userId, CancellationToken ct);
}
