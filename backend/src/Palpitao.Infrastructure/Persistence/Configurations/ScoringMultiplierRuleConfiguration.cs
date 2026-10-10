using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class ScoringMultiplierRuleConfiguration : IEntityTypeConfiguration<ScoringMultiplierRule>
{
    public void Configure(EntityTypeBuilder<ScoringMultiplierRule> builder)
    {
        builder.Property(x => x.Competition).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Phase).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => new { x.ConfigId, x.Competition, x.Phase }).IsUnique();
    }
}
