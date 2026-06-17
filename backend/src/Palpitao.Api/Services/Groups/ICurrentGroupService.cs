using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Groups;

/// <summary>
/// Resolves and validates the "current group" for the request from the
/// <c>X-Group-Id</c> header against the authenticated user's memberships. Never
/// trust the header alone — every method validates approved membership and throws
/// <see cref="Common.ForbiddenException"/> (HTTP 403) when access is not allowed.
/// </summary>
public interface ICurrentGroupService
{
    /// <summary>The current authenticated user id, or null if unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// True when the authenticated user is a platform SuperAdmin (global
    /// <see cref="UserRole.Admin"/>). A SuperAdmin gets <see cref="GroupRole.GroupAdmin"/>
    /// access to <em>any</em> existing group selected via the <c>X-Group-Id</c> header,
    /// without an explicit membership row.
    /// </summary>
    bool IsSuperAdmin { get; }

    /// <summary>Returns the current group id, validating approved membership. Throws when
    /// the header is missing/invalid or the user is not an approved member.</summary>
    Task<Guid> GetGroupIdAsync(CancellationToken ct);

    /// <summary>The user's role in the current group (validates membership first).</summary>
    Task<GroupRole> GetRoleAsync(CancellationToken ct);

    /// <summary>Ensures the user is an approved member of the current group (else 403).</summary>
    Task RequireApprovedMemberAsync(CancellationToken ct);

    /// <summary>Ensures the user is an approved <see cref="GroupRole.GroupAdmin"/> of the current group (else 403).</summary>
    Task RequireGroupAdminAsync(CancellationToken ct);
}
