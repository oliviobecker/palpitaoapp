using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>
/// Association between a global <see cref="User"/> and a <see cref="Group"/>,
/// carrying the user's role and approval status within that group. A user may
/// belong to more than one group (unique per (GroupId, UserId)).
/// </summary>
public class GroupUser
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public GroupRole Role { get; set; } = GroupRole.Participant;

    public GroupUserStatus Status { get; set; } = GroupUserStatus.PendingApproval;

    /// <summary>
    /// Per-group active flag. An admin can deactivate a participant in their own
    /// bolão without affecting the user's account or their membership in other
    /// groups. Inactive participants cannot submit predictions and are excluded
    /// from scoring/standings of this group.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Per-group elimination (e.g. 5th absence in this bolão). Independent per
    /// group — eliminated here does not eliminate the user elsewhere.
    /// </summary>
    public bool IsEliminated { get; set; }

    // --- Approval / rejection audit fields ----------------------------------
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public User? User { get; set; }
}
