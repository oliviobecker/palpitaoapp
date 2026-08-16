namespace Palpitao.Api.Entities;

/// <summary>
/// A name OCR reads off a screenshot that the admin has confirmed belongs to a participant —
/// a nickname the group uses ("Paraguaio"), a spelling the roster does not carry, or a stable
/// piece of junk a particular phone's rendering produces. Learned on confirmation and consulted
/// on the next import, so the same correction is not made twice.
///
/// Group-scoped (a nickname means different people in different pools) and keyed on the
/// normalized form, so casing and accents do not create duplicates.
/// </summary>
public class OcrParticipantAlias : IGroupOwned
{
    /// <summary>Widest name the parser can hand over — mirrors the candidate column.</summary>
    public const int MaxAliasLength = OcrPredictionCandidate.MaxParticipantNameLength;

    public Guid Id { get; set; }

    /// <summary>Owning group (tenant).</summary>
    public Guid GroupId { get; set; }

    /// <summary>Participant the alias resolves to.</summary>
    public Guid UserId { get; set; }

    /// <summary>Normalized lookup key (lowercased, accent-folded, spaces collapsed).</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The name exactly as OCR produced it, kept for the audit trail.</summary>
    public string AliasRaw { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public User? User { get; set; }
}
