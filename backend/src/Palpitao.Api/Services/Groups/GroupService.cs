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
                    AllowParticipantsToSubmitPredictions = g.AllowParticipantsToSubmitPredictions,
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
                AllowParticipantsToSubmitPredictions = gu.Group!.AllowParticipantsToSubmitPredictions,
            })
            .ToListAsync(ct);
    }

    public async Task<GroupSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        await _current.RequireGroupAdminAsync(ct);
        var groupId = await _current.GetGroupIdAsync(ct);
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new NotFoundException("notFound.group");

        return await ToSettingsDtoAsync(group, ct);
    }

    public async Task<GroupSettingsDto> UpdateSettingsAsync(UpdateGroupSettingsRequest request, CancellationToken ct)
    {
        await _current.RequireGroupAdminAsync(ct);
        var groupId = await _current.GetGroupIdAsync(ct);
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new NotFoundException("notFound.group");

        var changed = false;
        changed |= ApplySetting(group, nameof(Group.AllowParticipantsToViewOthersPredictions),
            group.AllowParticipantsToViewOthersPredictions, request.AllowParticipantsToViewOthersPredictions,
            v => group.AllowParticipantsToViewOthersPredictions = v);
        changed |= ApplySetting(group, nameof(Group.AllowParticipantsToSubmitPredictions),
            group.AllowParticipantsToSubmitPredictions, request.AllowParticipantsToSubmitPredictions,
            v => group.AllowParticipantsToSubmitPredictions = v);

        if (changed)
        {
            group.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return await ToSettingsDtoAsync(group, ct);
    }

    /// <summary>Applies a boolean setting, auditing the change; returns true when it changed.</summary>
    private bool ApplySetting(Group group, string setting, bool previous, bool next, Action<bool> assign)
    {
        if (previous == next)
        {
            return false;
        }

        assign(next);
        _audit.Add(_current.UserId ?? Guid.Empty, "GroupSettingsUpdated", nameof(Group), group.Id.ToString(),
            new { setting, previousValue = previous, newValue = next }, group.Id);
        return true;
    }

    private async Task<GroupSettingsDto> ToSettingsDtoAsync(Group group, CancellationToken ct)
        => new()
        {
            GroupId = group.Id,
            GroupName = group.Name,
            AllowParticipantsToViewOthersPredictions = group.AllowParticipantsToViewOthersPredictions,
            AllowParticipantsToSubmitPredictions = group.AllowParticipantsToSubmitPredictions,
            HasParticipantPredictions = await _db.Predictions.AnyAsync(
                p => p.Source == PredictionSource.Participant
                    && _db.Rounds.Any(r => r.Id == p.RoundId && r.GroupId == group.Id),
                ct),
        };
}
