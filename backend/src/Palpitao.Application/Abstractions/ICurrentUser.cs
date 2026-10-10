namespace Palpitao.Application.Abstractions;

/// <summary>
/// Who is calling and which group they asked for, as the transport reports it — nothing here is
/// validated. <c>CurrentGroupService</c> turns it into an approved membership or a 403.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or null for an anonymous call.</summary>
    Guid? UserId { get; }

    /// <summary>True when the caller holds the platform-wide super-admin role.</summary>
    bool IsSuperAdmin { get; }

    /// <summary>The group the caller asked to act in, exactly as sent (unparsed), or null.</summary>
    string? RequestedGroupId { get; }
}
