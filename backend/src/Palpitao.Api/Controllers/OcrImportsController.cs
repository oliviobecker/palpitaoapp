using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Palpitao.Api.Auth;
using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.Ocr;
using Sentry;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
[RequireGroupAdmin]
public class OcrImportsController : ControllerBase
{
    private readonly IOcrService _ocr;
    private readonly IPredictionImportService _import;

    public OcrImportsController(IOcrService ocr, IPredictionImportService import)
    {
        _ocr = ocr;
        _import = import;
    }

    [HttpPost("rounds/{roundId:guid}/predictions/import-image")]
    // 2 MB of headroom over the 10 MB image limit for the multipart envelope, so an
    // exactly-10MB file still reaches ValidateFile (which returns the localized error).
    [RequestSizeLimit(OcrService.MaxImageBytes + 2 * 1024 * 1024)]
    [EnableRateLimiting("ocr")]
    public async Task<ActionResult<OcrBatchDto>> Import(
        Guid roundId, [FromForm] IFormFile? file, [FromForm] string? language, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("ocr.sendImage");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var batch = await _ocr.ProcessAsync(roundId, file.FileName, ms.ToArray(), language, User.GetUserId(), ct);
        SentrySdk.AddBreadcrumb("OCR predictions imported.", "ocr", data: new Dictionary<string, string>
        {
            ["roundId"] = roundId.ToString(),
            ["batchId"] = batch.Id.ToString(),
        });
        return Ok(batch);
    }

    [HttpGet("rounds/{roundId:guid}/ocr-imports")]
    public async Task<ActionResult<List<OcrBatchSummaryDto>>> ListForRound(Guid roundId, CancellationToken ct)
        => Ok(await _ocr.ListBatchesAsync(roundId, ct));

    [HttpGet("ocr-imports/{batchId:guid}")]
    public async Task<ActionResult<OcrBatchDto>> Get(Guid batchId, CancellationToken ct)
        => Ok(await _ocr.GetBatchAsync(batchId, ct));

    /// <summary>Streams the stored upload. Separate rate-limit policy from the import itself:
    /// a gallery legitimately issues many reads, while uploads stay tightly capped.</summary>
    [HttpGet("ocr-imports/{batchId:guid}/image")]
    [EnableRateLimiting("ocrImage")]
    public async Task<IActionResult> GetImage(Guid batchId, CancellationToken ct)
    {
        var image = await _ocr.GetImageAsync(batchId, ct);

        // User-supplied bytes served from the API origin: refuse content sniffing and neuter any
        // polyglot that slipped past the header check. The filename is never echoed back.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        Response.Headers.ContentDisposition = "inline";
        // Authenticated and tenant-scoped, so never public. The bytes for a batch id never change.
        Response.Headers.CacheControl = "private, max-age=86400, immutable";

        // This overload handles If-None-Match / If-Modified-Since (304) and range requests.
        return File(
            image.Content,
            image.ContentType,
            lastModified: new DateTimeOffset(image.CreatedAt, TimeSpan.Zero),
            entityTag: new EntityTagHeaderValue($"\"{image.Sha256}\""));
    }

    [HttpPut("ocr-imports/{batchId:guid}/candidates/{candidateId:guid}")]
    public async Task<ActionResult<OcrBatchDto>> UpdateCandidate(
        Guid batchId, Guid candidateId, UpdateOcrCandidateRequest request, CancellationToken ct)
        => Ok(await _ocr.UpdateCandidateAsync(batchId, candidateId, request, User.GetUserId(), ct));

    [HttpDelete("ocr-imports/{batchId:guid}/candidates/{candidateId:guid}")]
    public async Task<ActionResult<OcrBatchDto>> DeleteCandidate(
        Guid batchId, Guid candidateId, CancellationToken ct)
        => Ok(await _ocr.DeleteCandidateAsync(batchId, candidateId, User.GetUserId(), ct));

    [HttpPost("ocr-imports/{batchId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid batchId, CancellationToken ct)
    {
        await _import.ConfirmAsync(batchId, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("ocr-imports/{batchId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid batchId, CancellationToken ct)
    {
        await _ocr.CancelAsync(batchId, User.GetUserId(), ct);
        return NoContent();
    }
}
