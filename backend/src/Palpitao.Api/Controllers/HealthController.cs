using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;

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
    private readonly AppDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
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
                    _logger.LogError("Migrations pendentes: {Pending}", string.Join(", ", pending));
                    return StatusCode(503, new { status = "migrations-pending", database = "postgres", pendingMigrations = pending });
                }
            }

            return Ok(new { status = "ok", database = "postgres" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check do banco falhou.");
            return StatusCode(503, new { status = "unavailable", database = "postgres", reason = ex.GetType().Name });
        }
    }
}
