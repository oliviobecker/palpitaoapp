using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Auth;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Auth;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Localization;
using Sentry;

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
    public async Task<ActionResult<MessageResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        await _auth.RegisterAsync(request, ct);
        _logger.LogInformation("Nova solicitação de cadastro: {Email} (grupo {GroupId})", request.Email, request.GroupId);
        SentrySdk.AddBreadcrumb("Registration submitted.", "auth", level: BreadcrumbLevel.Info);
        return Ok(new MessageResponse { Message = _localizer.Get("group.requestSubmitted") });
    }

    /// <summary>Public create-group flow: creates the group and its admin account. Does not authenticate.</summary>
    [HttpPost("create-group")]
    public async Task<ActionResult<MessageResponse>> CreateGroup(CreateGroupRequest request, CancellationToken ct)
    {
        await _auth.CreateGroupAsync(request, ct);
        _logger.LogInformation("Novo grupo criado por {Email}.", request.Email);
        SentrySdk.AddBreadcrumb("Group created.", "groups", level: BreadcrumbLevel.Info);
        return Ok(new MessageResponse { Message = _localizer.Get("group.created") });
    }

    /// <summary>Groups the authenticated user has approved access to.</summary>
    [HttpGet("my-groups")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MyGroupDto>>> MyGroups(CancellationToken ct)
        => Ok(await _groups.MyGroupsAsync(User.GetUserId(), User.IsInRole(UserRole.Admin.ToString()), ct));

    /// <summary>Authenticates a user and returns a JWT access token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Tentativa de login para {Email}", request.Email);

        var outcome = await _auth.LoginAsync(request, ct);
        if (outcome.Success)
        {
            _logger.LogInformation("Login bem-sucedido para {Email}.", request.Email);
            return Ok(outcome.Response);
        }

        var message = _localizer.Get(outcome.FailureKey!);
        if (outcome.InvalidCredentials)
        {
            _logger.LogWarning("Login falhou para {Email}: credenciais inválidas.", request.Email);
            return Unauthorized(new { message });
        }

        _logger.LogWarning("Login bloqueado para {Email}: {Reason}.", request.Email, outcome.FailureKey);
        SentrySdk.AddBreadcrumb("Login blocked by account status.", "auth", level: BreadcrumbLevel.Warning);
        return StatusCode(StatusCodes.Status403Forbidden, new { message });
    }
}
