using Palpitao.Api.DTOs.Auth;

namespace Palpitao.Api.Services.Auth;

/// <summary>Outcome of a login attempt. On failure, <see cref="FailureKey"/> is a
/// localizable message key; <see cref="InvalidCredentials"/> distinguishes a bad
/// password (401) from a blocked-but-valid account (403).</summary>
public record LoginOutcome(bool Success, string? FailureKey, bool InvalidCredentials, LoginResponse? Response)
{
    public static LoginOutcome Invalid() => new(false, "auth.invalidCredentials", true, null);
    public static LoginOutcome Blocked(string key) => new(false, key, false, null);
    public static LoginOutcome Ok(LoginResponse response) => new(true, null, false, response);
}

public interface IAuthService
{
    /// <summary>
    /// Group-aware self-registration: ensures a global account (created approved/active
    /// when new) and a <c>Participant</c> membership pending approval in the chosen
    /// group. No JWT.
    /// </summary>
    Task RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <summary>
    /// Public create-group flow: creates the admin account, the group and the
    /// creator's approved <c>GroupAdmin</c> membership. No JWT (user signs in next).
    /// </summary>
    Task CreateGroupAsync(DTOs.Groups.CreateGroupRequest request, CancellationToken ct);

    /// <summary>Authenticates a user, enforcing approval status and active flag.</summary>
    Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken ct);
}
