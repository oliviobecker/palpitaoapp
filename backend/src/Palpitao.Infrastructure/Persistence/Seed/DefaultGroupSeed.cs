using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Seed;

/// <summary>
/// The default group (tenant): owns all pre-existing data, and the dev admin is its GroupAdmin.
/// New groups are created at runtime via /auth/create-group.
/// </summary>
internal static class DefaultGroupSeed
{
    public static Group Group() => new()
    {
        Id = SeedIds.DefaultGroup,
        Name = "Palpitão England 2025/2026",
        Slug = "palpitao-england-2025-2026",
        Description = "Bolão da temporada inglesa.",
        CreatedByUserId = SeedIds.AdminUser,
        OwnerUserId = SeedIds.AdminUser,
        IsActive = true,
        CreatedAt = Seeded.At,
        UpdatedAt = Seeded.At,
    };

    public static GroupUser AdminMembership() => new()
    {
        Id = SeedIds.DefaultGroupAdminMembership,
        GroupId = SeedIds.DefaultGroup,
        UserId = SeedIds.AdminUser,
        Role = GroupRole.GroupAdmin,
        Status = GroupUserStatus.Approved,
        ApprovedAt = Seeded.At,
        CreatedAt = Seeded.At,
        UpdatedAt = Seeded.At,
    };
}
