namespace Palpitao.Api.Entities;

/// <summary>A single prediction candidate extracted by OCR, pending admin review.</summary>
public class OcrPredictionCandidate
{
    public Guid Id { get; set; }

    public Guid OcrImportBatchId { get; set; }

    public Guid RoundId { get; set; }

    /// <summary>Resolved participant (null when not matched / ambiguous).</summary>
    public Guid? UserId { get; set; }

    public string? ParticipantNameRaw { get; set; }

    /// <summary>Resolved match (null when not matched / ambiguous).</summary>
    public Guid? RoundMatchId { get; set; }

    public string? MatchTextRaw { get; set; }

    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }

    public double Confidence { get; set; }

    public bool NeedsReview { get; set; }

    public string? ReviewNotes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public OcrImportBatch? Batch { get; set; }
}
