using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Registrations;

public class RegistrationRequestService : IRegistrationRequestService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public RegistrationRequestService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task<IReadOnlyList<RegistrationRequestDto>> ListPendingAsync(CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.GroupUsers
            .Where(gu => gu.GroupId == groupId && gu.Status == GroupUserStatus.PendingApproval)
            .OrderBy(gu => gu.CreatedAt)
            .Join(_db.Users, gu => gu.UserId, u => u.Id, (gu, u) => new RegistrationRequestDto
            {
                Id = gu.Id,
                UserId = u.Id,
                Name = u.Name,
                Email = u.Email,
                CreatedAt = gu.CreatedAt,
                Status = gu.Status,
            })
            .ToListAsync(ct);
    }

    public async Task<RegistrationRequestDto> GetAsync(Guid groupUserId, CancellationToken ct)
    {
        var membership = await LoadAsync(groupUserId, requirePending: false, ct);
        var user = await _db.Users.FirstAsync(u => u.Id == membership.UserId, ct);
        return Map(membership, user);
    }

    public async Task ApproveAsync(Guid groupUserId, Guid adminId, CancellationToken ct)
    {
        var membership = await LoadAsync(groupUserId, requirePending: true, ct);

        var previous = membership.Status;
        var now = DateTime.UtcNow;
        membership.Status = GroupUserStatus.Approved;
        membership.ApprovedAt = now;
        membership.ApprovedByUserId = adminId;
        membership.RejectedAt = null;
        membership.RejectedByUserId = null;
        membership.RejectionReason = null;
        membership.UpdatedAt = now;

        _audit.Add(adminId, "RegistrationApproved", nameof(GroupUser), membership.Id.ToString(),
            new { membership.UserId, From = previous.ToString(), To = membership.Status.ToString() }, membership.GroupId);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(Guid groupUserId, string? reason, Guid adminId, CancellationToken ct)
    {
        var membership = await LoadAsync(groupUserId, requirePending: true, ct);

        var previous = membership.Status;
        var now = DateTime.UtcNow;
        membership.Status = GroupUserStatus.Rejected;
        membership.RejectedAt = now;
        membership.RejectedByUserId = adminId;
        membership.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        membership.UpdatedAt = now;

        _audit.Add(adminId, "RegistrationRejected", nameof(GroupUser), membership.Id.ToString(),
            new { membership.UserId, From = previous.ToString(), To = membership.Status.ToString(), membership.RejectionReason }, membership.GroupId);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Loads a membership ensuring it belongs to the current group.</summary>
    private async Task<GroupUser> LoadAsync(Guid groupUserId, bool requirePending, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var membership = await _db.GroupUsers
            .FirstOrDefaultAsync(gu => gu.Id == groupUserId && gu.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.user");

        if (requirePending && membership.Status != GroupUserStatus.PendingApproval)
        {
            throw new BusinessRuleException("registration.notPending");
        }

        return membership;
    }

    private static RegistrationRequestDto Map(GroupUser gu, User u) => new()
    {
        Id = gu.Id,
        UserId = u.Id,
        Name = u.Name,
        Email = u.Email,
        CreatedAt = gu.CreatedAt,
        Status = gu.Status,
    };
}
