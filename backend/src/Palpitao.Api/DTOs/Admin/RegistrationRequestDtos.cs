using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Admin;

public class RegistrationRequestDto
{
    /// <summary>Identifier of the membership request (GroupUser id).</summary>
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public GroupUserStatus Status { get; set; }
}

public class RejectRegistrationRequest
{
    /// <summary>Optional reason shown to the admin/audit log.</summary>
    public string? Reason { get; set; }
}
