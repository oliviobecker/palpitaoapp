using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Audit;

public interface IAuditService
{
    /// <summary>
    /// Adds an audit entry to the current unit of work (not saved here — the
    /// caller persists it together with the operation). Without an explicit
    /// <paramref name="groupId"/> the entry takes the group this request has already
    /// validated (<see cref="Groups.ICurrentGroupService.ResolvedGroupId"/>), or none
    /// outside a group request (login, background jobs).
    /// </summary>
    void Add(Guid? userId, string action, string entityName, string? entityId, object? details = null, Guid? groupId = null);

    /// <summary>Queries the audit log with optional filters (newest first). When
    /// <paramref name="groupId"/> is given, only that group's entries are returned.</summary>
    Task<IReadOnlyList<AuditLogDto>> QueryAsync(
        Guid? userId, string? entityName, DateTime? from, DateTime? to, CancellationToken ct, Guid? groupId = null);
}
