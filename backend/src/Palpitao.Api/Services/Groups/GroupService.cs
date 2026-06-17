using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Groups;

public class GroupService : IGroupService
{
    private readonly AppDbContext _db;

    public GroupService(AppDbContext db)
    {
        _db = db;
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
            })
            .ToListAsync(ct);
    }
}
