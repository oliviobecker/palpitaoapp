using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Admin;

public class OcrCandidateDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? ParticipantNameRaw { get; set; }
    public Guid? RoundMatchId { get; set; }
    public string? MatchTextRaw { get; set; }
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
    public double Confidence { get; set; }
    public bool NeedsReview { get; set; }
    public string? ReviewNotes { get; set; }
}

public class OcrBatchDto
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public OcrBatchStatus Status { get; set; }
    public string LanguageUsed { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public List<OcrCandidateDto> Candidates { get; set; } = new();
}

public class UpdateOcrCandidateRequest
{
    public Guid? UserId { get; set; }
    public Guid? RoundMatchId { get; set; }
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
    public string? ReviewNotes { get; set; }
}
