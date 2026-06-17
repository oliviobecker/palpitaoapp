using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Groups;

/// <summary>
/// Shared queries for resolving a group's roster. The set of "participants" of a
/// round/season is the group's approved <see cref="GroupRole.Participant"/>
/// members — not the global <c>User.Role</c> — so scoring/absences never reach
/// users from other groups.
/// </summary>
public static class GroupQueries
{
    /// <summary>Active, non-eliminated approved participant users of a group.
    /// Active/eliminated are per-group flags on <see cref="GroupUser"/>.</summary>
    public static IQueryable<User> ActiveParticipants(AppDbContext db, Guid groupId)
        => ApprovedMemberships(db, groupId)
            .Where(gu => gu.IsActive && !gu.IsEliminated)
            .Join(db.Users, gu => gu.UserId, u => u.Id, (gu, u) => u);

    /// <summary>All approved participant users of a group (including eliminated/inactive),
    /// e.g. for the predictions mirror which still lists eliminated members.</summary>
    public static IQueryable<User> AllParticipants(AppDbContext db, Guid groupId)
        => ApprovedMemberships(db, groupId)
            .Join(db.Users, gu => gu.UserId, u => u.Id, (gu, u) => u);

    /// <summary>Approved participant <see cref="GroupUser"/> memberships of a group
    /// (carrying the per-group <c>IsActive</c>/<c>IsEliminated</c> flags). Use when
    /// the caller needs those flags, not just the <see cref="User"/>.</summary>
    public static IQueryable<GroupUser> ApprovedMemberships(AppDbContext db, Guid groupId)
        => db.GroupUsers
            .Where(gu => gu.GroupId == groupId
                && gu.Status == GroupUserStatus.Approved
                && gu.Role == GroupRole.Participant);
}
