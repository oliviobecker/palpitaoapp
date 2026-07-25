namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Bound from the "OcrStorage" configuration section. Controls whether uploaded OCR images are
/// kept in the database and how aggressively they are pruned — the bytes live in Postgres, so
/// they show up in every backup and need a ceiling.
/// </summary>
public class OcrStorageOptions
{
    public const string SectionName = "OcrStorage";

    /// <summary>When false, images are not persisted. OCR itself still works — this is the
    /// operator kill switch if the database grows unexpectedly.</summary>
    public bool StoreImages { get; set; } = true;

    /// <summary>Delete stored bytes older than this many days (0 = keep forever).</summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>Keep at most this many stored images per round, oldest dropped first
    /// (0 = unlimited).</summary>
    public int MaxImagesPerRound { get; set; } = 10;
}
