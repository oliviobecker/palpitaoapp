using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.Registrations;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/registration-requests")]
[Authorize]
[RequireGroupAdmin]
public class AdminRegistrationRequestsController : ControllerBase
{
    private readonly IRegistrationRequestService _service;

    public AdminRegistrationRequestsController(IRegistrationRequestService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegistrationRequestDto>>> List(CancellationToken ct)
        => Ok(await _service.ListPendingAsync(ct));

    [HttpGet("{groupUserId:guid}")]
    public async Task<ActionResult<RegistrationRequestDto>> Get(Guid groupUserId, CancellationToken ct)
        => Ok(await _service.GetAsync(groupUserId, ct));

    [HttpPost("{groupUserId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid groupUserId, CancellationToken ct)
    {
        await _service.ApproveAsync(groupUserId, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("{groupUserId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid groupUserId, RejectRegistrationRequest? request, CancellationToken ct)
    {
        await _service.RejectAsync(groupUserId, request?.Reason, User.GetUserId(), ct);
        return NoContent();
    }
}
