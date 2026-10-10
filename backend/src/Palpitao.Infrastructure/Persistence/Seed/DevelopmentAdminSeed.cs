using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Seed;

/// <summary>The development admin user. Password: "Admin@123" (BCrypt). For local development only.</summary>
internal static class DevelopmentAdminSeed
{
    public static User User() => new()
    {
        Id = SeedIds.AdminUser,
        Name = "Administrador",
        Email = "admin@palpitao.local",
        PasswordHash = "$2a$11$rqqFHI1KeD4V96P8cdiPBeR8U8MEQEwED.AbOQ2aeuQeAeNJN3U.m",
        Role = UserRole.Admin,
        Status = UserStatus.Approved,
        IsActive = true,
        CreatedAt = Seeded.At,
    };
}
