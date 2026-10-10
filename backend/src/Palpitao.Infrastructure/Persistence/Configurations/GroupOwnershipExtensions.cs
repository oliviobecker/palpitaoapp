using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal static class GroupOwnershipExtensions
{
    /// <summary>
    /// Configures the tenant-ownership column shared by the <see cref="IGroupOwned"/> roots: a
    /// required <c>GroupId</c> FK to <see cref="Group"/> that defaults to the seeded default
    /// group. The default value keeps existing rows (and tests that don't set a group) attached
    /// to the default group while still enforcing referential integrity. The query filter that
    /// scopes reads to the request's group is applied to every <see cref="IGroupOwned"/> entity
    /// by <see cref="AppDbContext"/>.
    /// </summary>
    public static void HasGroupOwnership<T>(this EntityTypeBuilder<T> builder) where T : class, IGroupOwned
    {
        builder.Property("GroupId").HasDefaultValue(SeedIds.DefaultGroup);
        builder.HasIndex("GroupId");
        builder.HasOne(typeof(Group), "Group")
            .WithMany()
            .HasForeignKey("GroupId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
