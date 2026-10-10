using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
        // Existing rows (migration) default to Approved so current accounts keep
        // logging in; public sign-ups set PendingApproval explicitly.
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40)
            .HasDefaultValue(UserStatus.Approved);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Email).IsUnique();

        // The development admin is seeded at runtime, not here: see DevelopmentAdmin.
    }
}
