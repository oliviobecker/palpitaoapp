using Palpitao.Api.DTOs.Groups;

namespace Palpitao.Api.Services.Groups;

public interface IGroupService
{
    /// <summary>Active groups for the public registration picker.</summary>
    Task<IReadOnlyList<PublicGroupDto>> ListActiveAsync(CancellationToken ct);

    /// <summary>
    /// Groups the given user is an approved member of. A platform SuperAdmin
    /// (<paramref name="isSuperAdmin"/>) gets every group as <c>GroupAdmin</c>.
    /// </summary>
    Task<IReadOnlyList<MyGroupDto>> MyGroupsAsync(Guid userId, bool isSuperAdmin, CancellationToken ct);

    /// <summary>Current group's settings (group admin only).</summary>
    Task<GroupSettingsDto> GetSettingsAsync(CancellationToken ct);

    /// <summary>Updates the current group's settings (group admin only); audits changes.</summary>
    Task<GroupSettingsDto> UpdateSettingsAsync(UpdateGroupSettingsRequest request, CancellationToken ct);
}
