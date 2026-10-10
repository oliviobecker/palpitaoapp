using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class ScoringScoreEntryConfiguration : IEntityTypeConfiguration<ScoringScoreEntry>
{
    public void Configure(EntityTypeBuilder<ScoringScoreEntry> builder)
    {
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => new { x.ConfigId, x.Low, x.High }).IsUnique();
    }
}
