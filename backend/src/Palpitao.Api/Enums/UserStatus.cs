namespace Palpitao.Api.Enums;

/// <summary>
/// Approval lifecycle of a user account. Values are explicit and start at 1 so the
/// CLR default (0) is never a real status — this keeps EF Core's store default
/// ("Approved", for backfilling existing rows) from overriding an explicitly set
/// PendingApproval on insert.
/// </summary>
public enum UserStatus
{
    /// <summary>Self-registered, awaiting admin approval. Cannot log in.</summary>
    PendingApproval = 1,

    /// <summary>Approved by an admin. Can log in (when also active).</summary>
    Approved = 2,

    /// <summary>Rejected by an admin. Cannot log in.</summary>
    Rejected = 3,

    /// <summary>Deactivated. Cannot log in.</summary>
    Inactive = 4,
}
