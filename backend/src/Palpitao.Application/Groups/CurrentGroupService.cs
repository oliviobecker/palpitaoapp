using Microsoft.EntityFrameworkCore;
using Palpitao.Application.Abstractions;
using Palpitao.Application.Common.Exceptions;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Application.Groups;

/// <inheritdoc />
public class CurrentGroupService : ICurrentGroupService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    // Per-request cache of the resolved membership (the service is scoped). Assigned only
    // once every gate has passed, so it never holds a membership that was denied.
    private GroupUser? _resolved;
    private bool _resolvedOnce;

    public CurrentGroupService(IAppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public Guid? UserId => _user.UserId;

    /// <inheritdoc />
    public bool IsSuperAdmin => _user.IsSuperAdmin;

    /// <inheritdoc />
    public Guid? ResolvedGroupId => _resolved?.GroupId;

    public async Task<Guid> GetGroupIdAsync(CancellationToken ct)
        => (await ResolveAsync(ct)).GroupId;

    public async Task<GroupRole> GetRoleAsync(CancellationToken ct)
        => (await ResolveAsync(ct)).Role;

    public async Task RequireApprovedMemberAsync(CancellationToken ct)
        => await ResolveAsync(ct);

    public async Task RequireGroupAdminAsync(CancellationToken ct)
    {
        var membership = await ResolveAsync(ct);
        if (membership.Role != GroupRole.GroupAdmin)
        {
            throw new ForbiddenException("group.adminOnly");
        }
    }

    /// <summary>
    /// Resolves (and caches) the approved membership for the current user + the
    /// group from the <c>X-Group-Id</c> header. Throws 403 when missing/invalid or
    /// the user is not an approved member of that group.
    /// </summary>
    private async Task<GroupUser> ResolveAsync(CancellationToken ct)
    {
        if (_resolvedOnce)
        {
            return _resolved ?? throw new ForbiddenException();
        }

        _resolvedOnce = true;

        var userId = UserId;
        if (userId is null)
        {
            throw new ForbiddenException();
        }

        var header = _user.RequestedGroupId;
        if (string.IsNullOrWhiteSpace(header) || !Guid.TryParse(header, out var groupId))
        {
            throw new ForbiddenException("group.headerMissing");
        }

        var membership = await _db.GroupUsers
            .FirstOrDefaultAsync(
                gu => gu.GroupId == groupId
                    && gu.UserId == userId
                    && gu.Status == GroupUserStatus.Approved,
                ct);

        // A member deactivated in this group (per-group IsActive = false) is blocked
        // from the group entirely — not just excluded from scoring. SuperAdmins bypass.
        if (membership is not null && !membership.IsActive && !IsSuperAdmin)
        {
            throw new ForbiddenException("group.membershipInactive");
        }

        // Platform SuperAdmin: full GroupAdmin access to any existing group, even
        // without an explicit membership row. Still requires a valid header pointing
        // at a real group, so isolation for non-SuperAdmins is unaffected.
        if (membership is null
            && IsSuperAdmin
            && await _db.Groups.AnyAsync(g => g.Id == groupId, ct))
        {
            membership = new GroupUser
            {
                GroupId = groupId,
                UserId = userId.Value,
                Role = GroupRole.GroupAdmin,
                Status = GroupUserStatus.Approved,
            };
        }

        // Cached only now that every gate has passed: a denied membership must be neither
        // served by a later call nor reported by ResolvedGroupId.
        _resolved = membership;
        return _resolved ?? throw new ForbiddenException();
    }
}
