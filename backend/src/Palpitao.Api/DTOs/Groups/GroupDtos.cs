using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Groups;

/// <summary>Public, non-sensitive view of an active group (registration picker).</summary>
public class PublicGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TournamentType TournamentType { get; set; }
}

/// <summary>A group the authenticated user has approved access to.</summary>
public class MyGroupDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public GroupRole Role { get; set; }
    public GroupUserStatus Status { get; set; }
    public TournamentType TournamentType { get; set; }

    /// <summary>Whether participants may view others' predictions (drives the UI option).</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>Whether participants submit their own predictions in the app (drives the UI).</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;
}

/// <summary>Admin-facing group settings (read + update).</summary>
public class GroupSettingsDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public bool AllowParticipantsToViewOthersPredictions { get; set; }
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>
    /// True when at least one participant-submitted prediction already exists in the
    /// group — the UI warns before switching to admin-only.
    /// </summary>
    public bool HasParticipantPredictions { get; set; }
}

/// <summary>Update request for group settings (group admin).</summary>
public class UpdateGroupSettingsRequest
{
    public bool AllowParticipantsToViewOthersPredictions { get; set; }
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;
}

/// <summary>Public create-group request: creates the group and its admin account.</summary>
public class CreateGroupRequest
{
    public string GroupName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Required: which certame this group runs (England or FIFA World Cup).</summary>
    public TournamentType? TournamentType { get; set; }

    /// <summary>Optional: allow participants to view others' predictions (default false).</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>How predictions are submitted: participants in the app (default true) or admin-only.</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    public string AdminName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
