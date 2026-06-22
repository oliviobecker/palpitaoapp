using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.Users;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/users")]
[Authorize]
[RequireGroupAdmin]
public class AdminUsersController : ControllerBase
{
    private readonly IUserAdminService _users;

    public AdminUsersController(IUserAdminService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> List(CancellationToken ct)
        => Ok(await _users.ListParticipantsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ParticipantDto>> Create(CreateParticipantRequest request, CancellationToken ct)
        => Ok(await _users.CreateAsync(request, User.GetUserId(), ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ParticipantDto>> Update(Guid id, UpdateParticipantRequest request, CancellationToken ct)
        => Ok(await _users.UpdateAsync(id, request, User.GetUserId(), ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _users.SetActiveAsync(id, true, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _users.SetActiveAsync(id, false, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/eliminate")]
    public async Task<IActionResult> Eliminate(Guid id, EliminateRequest request, CancellationToken ct)
    {
        await _users.EliminateAsync(id, request.Justification, User.GetUserId(), ct);
        return NoContent();
    }
}
