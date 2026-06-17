using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;

namespace Palpitao.Api.Services.Groups;

public class GroupService : IGroupService
{
    private readonly AppDbContext _db;
    private readonly ICurrentGroupService _current;
    private readonly IAuditService _audit;

    public GroupService(AppDbContext db, ICurrentGroupService current, IAuditService audit)
    {
        _db = db;
        _current = current;
        _audit = audit;
    }

    public async Task<IReadOnlyList<PublicGroupDto>> ListActiveAsync(CancellationToken ct)
        => await _db.Groups
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new PublicGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Slug = g.Slug,
                Description = g.Description,
                TournamentType = g.TournamentType,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MyGroupDto>> MyGroupsAsync(Guid userId, bool isSuperAdmin, CancellationToken ct)
    {
        // Platform SuperAdmin sees and can act in every group (as GroupAdmin),
        // regardless of explicit memberships.
        if (isSuperAdmin)
        {
            return await _db.Groups
                .OrderBy(g => g.Name)
                .Select(g => new MyGroupDto
                {
                    GroupId = g.Id,
                    GroupName = g.Name,
                    Slug = g.Slug,
                    Role = GroupRole.GroupAdmin,
                    Status = GroupUserStatus.Approved,
                    TournamentType = g.TournamentType,
                    AllowParticipantsToViewOthersPredictions = g.AllowParticipantsToViewOthersPredictions,
                })
                .ToListAsync(ct);
        }

        return await _db.GroupUsers
            .Where(gu => gu.UserId == userId && gu.Status == GroupUserStatus.Approved)
            .OrderBy(gu => gu.Group!.Name)
            .Select(gu => new MyGroupDto
            {
                GroupId = gu.GroupId,
                GroupName = gu.Group!.Name,
                Slug = gu.Group!.Slug,
                Role = gu.Role,
                Status = gu.Status,
                TournamentType = gu.Group!.TournamentType,
                AllowParticipantsToViewOthersPredictions = gu.Group!.AllowParticipantsToViewOthersPredictions,
            })
            .ToListAsync(ct);
    }

    public async Task<GroupSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        await _current.RequireGroupAdminAsync(ct);
        var groupId = await _current.GetGroupIdAsync(ct);
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new NotFoundException("notFound.group");

        return new GroupSettingsDto
        {
            GroupId = group.Id,
            GroupName = group.Name,
            AllowParticipantsToViewOthersPredictions = group.AllowParticipantsToViewOthersPredictions,
        };
    }

    public async Task<GroupSettingsDto> UpdateSettingsAsync(UpdateGroupSettingsRequest request, CancellationToken ct)
    {
        await _current.RequireGroupAdminAsync(ct);
        var groupId = await _current.GetGroupIdAsync(ct);
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new NotFoundException("notFound.group");

        var previous = group.AllowParticipantsToViewOthersPredictions;
        if (previous != request.AllowParticipantsToViewOthersPredictions)
        {
            group.AllowParticipantsToViewOthersPredictions = request.AllowParticipantsToViewOthersPredictions;
            group.UpdatedAt = DateTime.UtcNow;

            _audit.Add(_current.UserId ?? Guid.Empty, "GroupSettingsUpdated", nameof(Group), group.Id.ToString(),
                new
                {
                    setting = nameof(Group.AllowParticipantsToViewOthersPredictions),
                    previousValue = previous,
                    newValue = group.AllowParticipantsToViewOthersPredictions,
                }, group.Id);

            await _db.SaveChangesAsync(ct);
        }

        return new GroupSettingsDto
        {
            GroupId = group.Id,
            GroupName = group.Name,
            AllowParticipantsToViewOthersPredictions = group.AllowParticipantsToViewOthersPredictions,
        };
    }
}
