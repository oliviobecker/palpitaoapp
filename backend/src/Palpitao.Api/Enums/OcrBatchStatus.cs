namespace Palpitao.Api.Enums;

public enum OcrBatchStatus
{
    Uploaded,
    Processed,
    Reviewed,
    Confirmed,

    /// <summary>OCR itself blew up — the image never produced usable text.</summary>
    Failed,

    /// <summary>
    /// The admin discarded the review. Distinct from <see cref="Failed"/>: both used to be
    /// stored as Failed, which made the import history read as if the OCR had broken on images
    /// that were simply re-sent. Batches cancelled before this split keep the old Failed value.
    /// </summary>
    Cancelled,
}
