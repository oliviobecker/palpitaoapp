using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Audit;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public void Add(Guid? userId, string action, string entityName, string? entityId, object? details = null, Guid? groupId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
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
