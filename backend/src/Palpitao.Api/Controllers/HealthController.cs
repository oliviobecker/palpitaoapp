using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.Services.Ocr;

namespace Palpitao.Api.Controllers;

/// <summary>
/// Liveness/readiness endpoints. Used to confirm the API is up and that the
/// initial PostgreSQL connection is working. Public (no auth) on purpose so a
/// deploy/monitor can probe an unauthenticated, possibly db-less API.
/// </summary>
[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    // Every language the OCR import screen offers (see OcrService.NormalizeLanguage).
    private const string OcrLanguages = "por+eng";

    private readonly AppDbContext _db;
    private readonly IOcrEngine _ocr;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, IOcrEngine ocr, ILogger<HealthController> logger)
    {
        _db = db;
        _ocr = ocr;
        _logger = logger;
    }

    /// <summary>Basic liveness check — the API process is responding.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "Palpitao.Api" });

    /// <summary>Readiness check — verifies connectivity to the database.</summary>
    [HttpGet("db")]
    public async Task<IActionResult> Database(CancellationToken cancellationToken)
    {
        try
        {
            // CanConnectAsync can throw (not just return false) on auth/host/DNS
            // failures, so wrap it to always return a clean 503 instead of a 500.
            if (!await _db.Database.CanConnectAsync(cancellationToken))
            {
                return StatusCode(503, new { status = "unavailable", database = "postgres" });
            }

            // Schema drift detection: a deploy that didn't run migrations leaves the app
            // serving a stale schema (queries on new columns 500). Only meaningful for a
            // migrated database — tests/dev use EnsureCreated (no migration history), so
            // skip when nothing is recorded as applied.
            var applied = await _db.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (applied.Any())
            {
                var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count > 0)
                {
                    // Log the specifics for operators; don't expose schema/migration
                    // names on this anonymous endpoint.
                    _logger.LogError("Migrations pendentes: {Pending}", string.Join(", ", pending));
                    return StatusCode(503, new { status = "migrations-pending", database = "postgres" });
                }
            }

            return Ok(new { status = "ok", database = "postgres" });
        }
        catch (Exception ex)
        {
            // The exception detail goes to the logs only, not the anonymous response body.
            _logger.LogError(ex, "Health check do banco falhou.");
            return StatusCode(503, new { status = "unavailable", database = "postgres" });
        }
    }

    /// <summary>
    /// Readiness check for the OCR import — the Tesseract language models are in place. Its own
    /// endpoint because they ship as plain files copied by the deploy: they can go missing while
    /// the API is healthy in every other way, and the failure would otherwise only surface when
    /// an admin uploads a screenshot.
    /// </summary>
    [HttpGet("ocr")]
    public IActionResult Ocr()
    {
        var missing = _ocr.MissingLanguages(OcrLanguages);

        // The engine logs the resolved tessdata path; this anonymous response only names the
        // language codes, never a server path.
        return missing.Count > 0
            ? StatusCode(503, new { status = "unavailable", missing })
            : Ok(new { status = "ok", languages = OcrLanguages.Split('+') });
    }
}
