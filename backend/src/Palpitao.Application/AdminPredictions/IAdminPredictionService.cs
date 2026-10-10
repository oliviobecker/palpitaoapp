using Palpitao.Application.Audit;
using Palpitao.Application.Flavio;
using Palpitao.Application.Ocr;
using Palpitao.Application.Registrations;
using Palpitao.Application.Users;

namespace Palpitao.Application.AdminPredictions;

public interface IAdminPredictionService
{
    /// <summary>Registers/edits a participant's predictions on behalf of them (admin).</summary>
    Task SaveManualAsync(Guid roundId, ManualPredictionRequest request, Guid adminId, CancellationToken ct);

    /// <summary>A participant's current predictions for a round, to preload the manual screen.</summary>
    Task<AdminParticipantPredictionsDto> GetParticipantPredictionsAsync(Guid roundId, Guid userId, CancellationToken ct);

    /// <summary>Prediction coverage of the round: who has all matches predicted, who is missing.</summary>
    Task<PredictionCoverageDto> GetCoverageAsync(Guid roundId, CancellationToken ct);
}
