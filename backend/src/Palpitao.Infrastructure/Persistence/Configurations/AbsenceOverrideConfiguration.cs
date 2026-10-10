using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class AbsenceOverrideConfiguration : IEntityTypeConfiguration<AbsenceOverride>
{
    public void Configure(EntityTypeBuilder<AbsenceOverride> builder)
    {
        builder.Property(x => x.Justification).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.RoundId, x.UserId }).IsUnique();

        builder.HasOne(x => x.Round)
            .WithMany()
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
