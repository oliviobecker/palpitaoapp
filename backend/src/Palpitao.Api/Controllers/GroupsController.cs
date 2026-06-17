using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Controllers;

/// <summary>Public, unauthenticated group discovery (registration picker).</summary>
[ApiController]
[Route("public/groups")]
[AllowAnonymous]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groups;

    public GroupsController(IGroupService groups)
    {
        _groups = groups;
    }

    /// <summary>Lists active groups (id, name, slug, description).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PublicGroupDto>>> List(CancellationToken ct)
        => Ok(await _groups.ListActiveAsync(ct));
}
