using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Seed;

/// <summary>
/// The default group (tenant): owns all pre-existing data. New groups are created at runtime via
/// /auth/create-group. Its admin, the development admin, is seeded separately
/// (<see cref="DevelopmentAdmin"/>).
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
}
