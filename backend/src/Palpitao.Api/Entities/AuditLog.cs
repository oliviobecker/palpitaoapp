namespace Palpitao.Api.Entities;

/// <summary>
/// Generic audit trail for administrative/relevant actions (round publishing,
/// scoring, penalties, etc.).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant). Null for global/pre-group events (login,
    /// registration, group creation) that are not scoped to a single group.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>User who performed the action (null for system actions).</summary>
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    /// <summary>Optional extra context (stored as JSON).</summary>
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
