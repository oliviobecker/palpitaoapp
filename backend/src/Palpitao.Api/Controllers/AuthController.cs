using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Palpitao.Api.Auth;
using Palpitao.Api.Extensions;
using Palpitao.Application.Abstractions;
using Palpitao.Application.Auth;
using Palpitao.Application.Groups;
using Palpitao.Domain.Enums;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IGroupService _groups;
    private readonly ILogger<AuthController> _logger;
    private readonly ILocalizationService _localizer;

    public AuthController(IAuthService auth, IGroupService groups, ILogger<AuthController> logger, ILocalizationService localizer)
    {
        _auth = auth;
        _groups = groups;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>Public self-registration into a group. Creates a pending membership; does not authenticate.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<MessageResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        await _auth.RegisterAsync(request, ct);
        _logger.LogInformation(
            "Registration requested by {Email} for group {GroupId}.", LogRedaction.Email(request.Email), request.GroupId);
        SentrySdk.AddBreadcrumb("Registration submitted.", "auth", level: BreadcrumbLevel.Info);
        return Ok(new MessageResponse { Message = _localizer.Get("group.requestSubmitted") });
    }

    /// <summary>Public create-group flow: creates the group and its admin account. Does not authenticate.</summary>
    [HttpPost("create-group")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<MessageResponse>> CreateGroup(CreateGroupRequest request, CancellationToken ct)
    {
        await _auth.CreateGroupAsync(request, ct);
        _logger.LogInformation("Group created by {Email}.", LogRedaction.Email(request.Email));
        SentrySdk.AddBreadcrumb("Group created.", "groups", level: BreadcrumbLevel.Info);
        return Ok(new MessageResponse { Message = _localizer.Get("group.created") });
    }

    /// <summary>Groups the authenticated user has approved access to.</summary>
    [HttpGet("my-groups")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MyGroupDto>>> MyGroups(CancellationToken ct)
        => Ok(await _groups.MyGroupsAsync(User.GetUserId(), User.IsInRole(UserRole.Admin.ToString()), ct));

    /// <summary>The user's not-yet-approved memberships (pending/rejected), for the awaiting-approval screen.</summary>
    [HttpGet("my-groups/pending")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MyGroupDto>>> PendingGroups(CancellationToken ct)
        => Ok(await _groups.PendingMembershipsAsync(User.GetUserId(), ct));

    /// <summary>Authenticates a user and returns a JWT access token.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Login attempt for {Email}.", LogRedaction.Email(request.Email));

        var outcome = await _auth.LoginAsync(request, ct);
        if (outcome.Success)
        {
            _logger.LogInformation("Login succeeded for {Email}.", LogRedaction.Email(request.Email));
            return Ok(outcome.Response);
        }

        var message = _localizer.Get(outcome.FailureKey!);
        if (outcome.InvalidCredentials)
        {
            _logger.LogWarning("Login failed for {Email}: invalid credentials.", LogRedaction.Email(request.Email));
            return Problem(detail: message, statusCode: StatusCodes.Status401Unauthorized);
        }

        _logger.LogWarning("Login blocked for {Email}: {Reason}.", LogRedaction.Email(request.Email), outcome.FailureKey);
        SentrySdk.AddBreadcrumb("Login blocked by account status.", "auth", level: BreadcrumbLevel.Warning);
        return Problem(detail: message, statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>Exchanges a refresh token for a new access token and a rotated refresh token.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var outcome = await _auth.RefreshAsync(request.RefreshToken, ct);
        if (outcome.Success)
        {
            return Ok(outcome.Response);
        }

        SentrySdk.AddBreadcrumb("Refresh token rejected.", "auth", level: BreadcrumbLevel.Warning);
        return Problem(detail: _localizer.Get(outcome.FailureKey!), statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>Revokes a refresh token (logout). Idempotent for unknown/expired tokens.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
