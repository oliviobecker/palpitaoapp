using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Tests.TestSupport;

/// <summary>Helpers for seeding group memberships in service unit tests.</summary>
public static class TestSeed
{
    /// <summary>Adds an approved participant membership in the default group (does not SaveChanges).
    /// Per-group active/eliminated flags now live on the membership.</summary>
    public static void AddDefaultGroupMembership(AppDbContext db, Guid userId,
        GroupRole role = GroupRole.Participant, GroupUserStatus status = GroupUserStatus.Approved,
        bool isActive = true, bool isEliminated = false)
    {
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = SeedIds.DefaultGroup,
            UserId = userId,
            Role = role,
            Status = status,
            IsActive = isActive,
            IsEliminated = isEliminated,
            ApprovedAt = status == GroupUserStatus.Approved ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Reads a user's per-group eliminated flag in the default group.</summary>
    public static bool IsEliminatedInDefaultGroup(AppDbContext db, Guid userId)
        => db.GroupUsers.Single(gu => gu.GroupId == SeedIds.DefaultGroup && gu.UserId == userId).IsEliminated;

    /// <summary>Reads a user's per-group active flag in the default group.</summary>
    public static bool IsActiveInDefaultGroup(AppDbContext db, Guid userId)
        => db.GroupUsers.Single(gu => gu.GroupId == SeedIds.DefaultGroup && gu.UserId == userId).IsActive;
}
