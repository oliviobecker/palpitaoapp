namespace Palpitao.Api.Entities;

/// <summary>
/// The stored bytes of an OCR upload. Kept in a side table rather than as a column on
/// <see cref="OcrImportBatch"/> because EF emits <c>SELECT *</c> for entity queries and has no
/// lazy scalars — a blob on the batch would be loaded by every candidate autosave. It also lets
/// retention drop the bytes without touching the batch, its candidates or the audit trail.
/// </summary>
public class OcrImportImage
{
    /// <summary>Shared primary key with <see cref="OcrImportBatch"/> (1:1).</summary>
    public Guid OcrImportBatchId { get; set; }

    public byte[] Content { get; set; } = [];

    /// <summary>Sniffed from the file header, never taken from the client.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Canonical extension for the sniffed type (".png"/".jpg"/".webp").</summary>
    public string FileExtension { get; set; } = string.Empty;

    public int ByteSize { get; set; }

    /// <summary>Lowercase hex SHA-256 of <see cref="Content"/>; also serves as the HTTP ETag.</summary>
    public string Sha256 { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Navigation
    public OcrImportBatch? Batch { get; set; }
}
