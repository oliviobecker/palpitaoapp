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
}

/// <summary>Public create-group request: creates the group and its admin account.</summary>
public class CreateGroupRequest
{
    public string GroupName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Required: which certame this group runs (England or FIFA World Cup).</summary>
    public TournamentType? TournamentType { get; set; }

    public string AdminName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
