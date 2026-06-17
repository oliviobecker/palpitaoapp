using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            if (await _db.Database.CanConnectAsync(cancellationToken))
            {
                return Ok(new { status = "ok", database = "postgres" });
            }

            return StatusCode(503, new { status = "unavailable", database = "postgres" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check do banco falhou.");
            return StatusCode(503, new { status = "unavailable", database = "postgres", reason = ex.GetType().Name });
        }
    }
}
