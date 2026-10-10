using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class RoundConfiguration : IEntityTypeConfiguration<Round>
{
    public void Configure(EntityTypeBuilder<Round> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(160);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        // Part 0 = standalone round; the parts of a round played in parts share its Number.
        builder.HasIndex(x => new { x.SeasonId, x.Number, x.Part }).IsUnique();
        builder.HasGroupOwnership();

        builder.HasOne(x => x.Season)
            .WithMany(s => s.Rounds)
            .HasForeignKey(x => x.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
