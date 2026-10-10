using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Seed;

/// <summary>
/// The development admin: <c>admin@palpitao.local</c>, a platform admin and the default group's
/// GroupAdmin, whose password is printed in <c>docs/development.md</c>. It is not part of the EF
/// model's seed — a credential published in the repository does not belong in every environment's
/// schema. Development seeds it into an empty database at startup; the unit tests seed it into
/// theirs.
/// </summary>
/// <remarks>
/// Databases created through the migrations still get this account from the historical
/// <c>InitialCreate</c> and <c>AddGroupsAndTenancy</c> migrations — which is what keeps a fresh
/// environment bootstrappable — so <see cref="HasPublishedPassword"/> lets the host flag one whose
/// password was never changed.
/// </remarks>
public static class DevelopmentAdmin
{
    /// <summary>BCrypt hash of the development password published in the repository.</summary>
    private const string PublishedPasswordHash = "$2a$11$rqqFHI1KeD4V96P8cdiPBeR8U8MEQEwED.AbOQ2aeuQeAeNJN3U.m";

    /// <summary>
    /// Inserts the admin and its membership in the default group — only into a database with no
    /// users at all, so it can never bring the account back on a populated database (a Development
    /// profile may point at a shared one).
    /// </summary>
    /// <returns>Whether anything was inserted.</returns>
    public static bool SeedIfNoUsers(AppDbContext db)
    {
        if (db.Users.Any())
        {
            return false;
        }

        db.Users.Add(new User
        {
            Id = SeedIds.AdminUser,
            Name = "Administrador",
            Email = "admin@palpitao.local",
            PasswordHash = PublishedPasswordHash,
            Role = UserRole.Admin,
            Status = UserStatus.Approved,
            IsActive = true,
            CreatedAt = Seeded.At,
        });
        db.GroupUsers.Add(new GroupUser
        {
            Id = SeedIds.DefaultGroupAdminMembership,
            GroupId = SeedIds.DefaultGroup,
            UserId = SeedIds.AdminUser,
            Role = GroupRole.GroupAdmin,
            Status = GroupUserStatus.Approved,
            ApprovedAt = Seeded.At,
            CreatedAt = Seeded.At,
            UpdatedAt = Seeded.At,
        });
        db.SaveChanges();
        return true;
    }

    /// <summary>Whether the seeded admin still signs in with the password published in the repository.</summary>
    public static bool HasPublishedPassword(AppDbContext db) =>
        db.Users.Any(u => u.Id == SeedIds.AdminUser && u.PasswordHash == PublishedPasswordHash);
}
