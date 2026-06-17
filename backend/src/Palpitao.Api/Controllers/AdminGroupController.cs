using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Controllers;

/// <summary>Group settings for the current group's admin (e.g. prediction visibility).</summary>
[ApiController]
[Route("admin/group")]
[Authorize]
[RequireGroupAdmin]
public class AdminGroupController : ControllerBase
{
    private readonly IGroupService _groups;

    public AdminGroupController(IGroupService groups)
    {
        _groups = groups;
    }

    /// <summary>Current group's settings.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<GroupSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _groups.GetSettingsAsync(ct));

    /// <summary>Updates the current group's settings (audited).</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<GroupSettingsDto>> UpdateSettings(UpdateGroupSettingsRequest request, CancellationToken ct)
        => Ok(await _groups.UpdateSettingsAsync(request, ct));
}
