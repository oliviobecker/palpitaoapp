using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class SeasonScoringConfigConfiguration : IEntityTypeConfiguration<SeasonScoringConfig>
{
    public void Configure(EntityTypeBuilder<SeasonScoringConfig> builder)
    {
        // One editable ruleset per season; a tenant root (carries GroupId).
        builder.HasIndex(x => x.SeasonId).IsUnique();
        builder.HasGroupOwnership();

        builder.HasOne(x => x.Season)
            .WithOne()
            .HasForeignKey<SeasonScoringConfig>(x => x.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ScoreEntries)
            .WithOne(x => x.Config)
            .HasForeignKey(x => x.ConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.MultiplierRules)
            .WithOne(x => x.Config)
            .HasForeignKey(x => x.ConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ClassicTeams)
            .WithOne(x => x.Config)
            .HasForeignKey(x => x.ConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
