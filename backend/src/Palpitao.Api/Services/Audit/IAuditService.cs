using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Audit;

public interface IAuditService
{
    /// <summary>
    /// Adds an audit entry to the current unit of work (not saved here — the
    /// caller persists it together with the operation).
    /// </summary>
    void Add(Guid? userId, string action, string entityName, string? entityId, object? details = null, Guid? groupId = null);

    /// <summary>Queries the audit log with optional filters (newest first). When
    /// <paramref name="groupId"/> is given, only that group's entries are returned.</summary>
    Task<IReadOnlyList<AuditLogDto>> QueryAsync(
        Guid? userId, string? entityName, DateTime? from, DateTime? to, CancellationToken ct, Guid? groupId = null);
}
