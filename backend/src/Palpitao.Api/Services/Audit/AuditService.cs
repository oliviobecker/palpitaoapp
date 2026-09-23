using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Audit;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentGroupService? _currentGroup;

    /// <param name="db">The unit of work the entries are added to.</param>
    /// <param name="currentGroup">
    /// Supplies the group the request has already validated, stamped on entries whose caller
    /// passes no group. The DI container always injects it; it is optional only so unit tests
    /// that do not care about the group can omit it (their entries stay unscoped).
    /// </param>
    public AuditService(AppDbContext db, ICurrentGroupService? currentGroup = null)
    {
        _db = db;
        _currentGroup = currentGroup;
    }

    public void Add(Guid? userId, string action, string entityName, string? entityId, object? details = null, Guid? groupId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            // An explicit group wins; otherwise the group this request already validated —
            // never the raw X-Group-Id header, so a forged header cannot plant entries in
            // another group's trail. Outside a group request (login, background jobs) it
            // stays null: a global event.
            GroupId = groupId ?? _currentGroup?.ResolvedGroupId,
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details),
            CreatedAt = DateTime.UtcNow,
        });
    }

    public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(
        Guid? userId, string? entityName, DateTime? from, DateTime? to, CancellationToken ct, Guid? groupId = null)
    {
        var query = _db.AuditLogs.AsNoTracking();

        if (groupId is not null)
        {
            query = query.Where(a => a.GroupId == groupId);
        }
        if (userId is not null)
        {
            query = query.Where(a => a.UserId == userId);
        }
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName == entityName);
        }
        if (from is not null)
        {
            query = query.Where(a => a.CreatedAt >= from);
        }
        if (to is not null)
        {
            query = query.Where(a => a.CreatedAt <= to);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = _db.Users.Where(u => u.Id == a.UserId).Select(u => u.Name).FirstOrDefault(),
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Details = a.Details,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(ct);
    }
}
