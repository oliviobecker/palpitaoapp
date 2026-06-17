using Palpitao.Api.Data;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="ICurrentGroupService"/> that resolves a fixed group
/// (the seeded default group by default) without an HTTP context. Used by service
/// unit tests that exercise group-scoped queries.
/// </summary>
public sealed class FakeCurrentGroupService : ICurrentGroupService
{
    private readonly Guid _groupId;
    private readonly GroupRole _role;

    public FakeCurrentGroupService(Guid? groupId = null, GroupRole role = GroupRole.GroupAdmin, Guid? userId = null, bool isSuperAdmin = false)
    {
        _groupId = groupId ?? SeedIds.DefaultGroup;
        _role = role;
        UserId = userId;
        IsSuperAdmin = isSuperAdmin;
    }

    public Guid? UserId { get; }

    public bool IsSuperAdmin { get; }

    public Task<Guid> GetGroupIdAsync(CancellationToken ct) => Task.FromResult(_groupId);

    public Task<GroupRole> GetRoleAsync(CancellationToken ct) => Task.FromResult(_role);

    public Task RequireApprovedMemberAsync(CancellationToken ct) => Task.CompletedTask;

    public Task RequireGroupAdminAsync(CancellationToken ct) => Task.CompletedTask;
}
