using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Ocr;

public interface IOcrService
{
    /// <summary>Validates and OCR-processes an uploaded image, producing candidates.</summary>
    Task<OcrBatchDto> ProcessAsync(
        Guid roundId, string fileName, byte[] bytes, string? language, Guid adminId, CancellationToken ct);

    Task<OcrBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct);

    Task<OcrBatchDto> UpdateCandidateAsync(
        Guid batchId, Guid candidateId, UpdateOcrCandidateRequest request, Guid adminId, CancellationToken ct);

    /// <summary>Discards a candidate (e.g. OCR noise) so it no longer blocks confirmation.</summary>
    Task<OcrBatchDto> DeleteCandidateAsync(Guid batchId, Guid candidateId, Guid adminId, CancellationToken ct);

    Task CancelAsync(Guid batchId, Guid adminId, CancellationToken ct);
}
