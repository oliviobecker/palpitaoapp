using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>An OCR import job: one uploaded image processed for a round.</summary>
public class OcrImportBatch
{
    public Guid Id { get; set; }

    public Guid RoundId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string? StoredFilePath { get; set; }

    public string? ExtractedText { get; set; }

    public string LanguageUsed { get; set; } = "por";

    public OcrBatchStatus Status { get; set; } = OcrBatchStatus.Uploaded;

    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    // Navigation
    public Round? Round { get; set; }
    public ICollection<OcrPredictionCandidate> Candidates { get; set; } = new List<OcrPredictionCandidate>();
}
