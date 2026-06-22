using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/audit")]
[Authorize]
[RequireGroupAdmin]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public AdminAuditController(IAuditService audit, ICurrentGroupService current)
    {
        _audit = audit;
        _current = current;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> Query(
        [FromQuery] Guid? userId,
        [FromQuery] string? entityName,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return Ok(await _audit.QueryAsync(userId, entityName, from, to, ct, groupId));
    }
}
