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

    /// <summary>Whether the uploaded image is still stored (false for pre-feature or pruned batches).</summary>
    public bool HasImage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public List<OcrCandidateDto> Candidates { get; set; } = new();
}

/// <summary>One past import of a round. Deliberately omits ExtractedText (large) and the bytes.</summary>
public class OcrBatchSummaryDto
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public OcrBatchStatus Status { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string LanguageUsed { get; set; } = string.Empty;
    public bool HasImage { get; set; }
    public string? ImageContentType { get; set; }
    public int? ImageByteSize { get; set; }
    public int CandidateCount { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public class UpdateOcrCandidateRequest
{
    public Guid? UserId { get; set; }
    public Guid? RoundMatchId { get; set; }
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
    public string? ReviewNotes { get; set; }
}

/// <summary>A name confirmed for a participant, as shown on the admin screen.</summary>
public class OcrParticipantAliasDto
{
    public Guid Id { get; set; }

    /// <summary>Normalized lookup key — shown only as a hint of what actually matches.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The name as it was written/read, which is what the admin recognises.</summary>
    public string AliasRaw { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateOcrParticipantAliasRequest
{
    public string AliasRaw { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}

/// <summary>
/// Only the participant can be re-pointed. Changing the text would re-key the row against the
/// unique (GroupId, Alias) index — delete and create instead.
/// </summary>
public class UpdateOcrParticipantAliasRequest
{
    public Guid UserId { get; set; }
}
