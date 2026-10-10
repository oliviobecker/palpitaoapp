using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class GroupUserConfiguration : IEntityTypeConfiguration<GroupUser>
{
    public void Configure(EntityTypeBuilder<GroupUser> builder)
    {
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        // Existing memberships default to active/not-eliminated on migration.
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.IsEliminated).HasDefaultValue(false);
        // A user has at most one membership per group.
        builder.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.GroupId, x.Status });

        builder.HasOne(x => x.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(DefaultGroupSeed.AdminMembership());
    }
}
