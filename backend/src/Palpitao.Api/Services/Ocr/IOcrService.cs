using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Ocr;

/// <summary>The stored bytes of an upload, ready to be written to the response.</summary>
public sealed record OcrImageContent(byte[] Content, string ContentType, string Sha256, DateTime CreatedAt);

public interface IOcrService
{
    /// <summary>Validates and OCR-processes an uploaded image, producing candidates.</summary>
    Task<OcrBatchDto> ProcessAsync(
        Guid roundId, string fileName, byte[] bytes, string? language, Guid adminId, CancellationToken ct);

    Task<OcrBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct);

    /// <summary>Past imports of a round, newest first. Never loads the image bytes.</summary>
    Task<List<OcrBatchSummaryDto>> ListBatchesAsync(Guid roundId, CancellationToken ct);

    /// <summary>The stored image of a batch (404 when the batch has none).</summary>
    Task<OcrImageContent> GetImageAsync(Guid batchId, CancellationToken ct);

    Task<OcrBatchDto> UpdateCandidateAsync(
        Guid batchId, Guid candidateId, UpdateOcrCandidateRequest request, Guid adminId, CancellationToken ct);

    /// <summary>Discards a candidate (e.g. OCR noise) so it no longer blocks confirmation.</summary>
    Task<OcrBatchDto> DeleteCandidateAsync(Guid batchId, Guid candidateId, Guid adminId, CancellationToken ct);

    Task CancelAsync(Guid batchId, Guid adminId, CancellationToken ct);
}
