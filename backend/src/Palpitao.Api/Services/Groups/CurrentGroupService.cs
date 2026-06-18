using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Groups;

/// <inheritdoc />
public class CurrentGroupService : ICurrentGroupService
{
    public const string GroupHeader = "X-Group-Id";

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    // Per-request cache of the resolved membership (the service is scoped).
    private GroupUser? _resolved;
    private bool _resolvedOnce;

    public CurrentGroupService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public Guid? UserId
    {
        get
        {
            var value = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public bool IsSuperAdmin
        => _http.HttpContext?.User.IsInRole(UserRole.Admin.ToString()) == true;

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

        var header = _http.HttpContext?.Request.Headers[GroupHeader].ToString();
        if (string.IsNullOrWhiteSpace(header) || !Guid.TryParse(header, out var groupId))
        {
            throw new ForbiddenException("group.headerMissing");
        }

        _resolved = await _db.GroupUsers
            .FirstOrDefaultAsync(
                gu => gu.GroupId == groupId
                    && gu.UserId == userId
                    && gu.Status == GroupUserStatus.Approved,
                ct);

        // A member deactivated in this group (per-group IsActive = false) is blocked
        // from the group entirely — not just excluded from scoring. SuperAdmins bypass.
        if (_resolved is not null && !_resolved.IsActive && !IsSuperAdmin)
        {
            throw new ForbiddenException("group.membershipInactive");
        }

        // Platform SuperAdmin: full GroupAdmin access to any existing group, even
        // without an explicit membership row. Still requires a valid header pointing
        // at a real group, so isolation for non-SuperAdmins is unaffected.
        if (_resolved is null
            && IsSuperAdmin
            && await _db.Groups.AnyAsync(g => g.Id == groupId, ct))
        {
            _resolved = new GroupUser
            {
                GroupId = groupId,
                UserId = userId.Value,
                Role = GroupRole.GroupAdmin,
                Status = GroupUserStatus.Approved,
            };
        }

        return _resolved ?? throw new ForbiddenException();
    }
}
