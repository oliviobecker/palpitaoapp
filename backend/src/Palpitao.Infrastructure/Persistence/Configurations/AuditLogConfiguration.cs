using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(80);
        builder.Property(x => x.Details).HasColumnType("jsonb");
        // The admin audit list filters by group and orders by CreatedAt desc;
        // a composite index serves both (its GroupId prefix also covers
        // group-only lookups). EntityName supports the entity filter.
        builder.HasIndex(x => new { x.GroupId, x.CreatedAt });
        builder.HasIndex(x => x.EntityName);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
