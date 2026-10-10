using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class ScoringClassicTeamConfiguration : IEntityTypeConfiguration<ScoringClassicTeam>
{
    public void Configure(EntityTypeBuilder<ScoringClassicTeam> builder)
    {
        builder.Property(x => x.Competition).HasConversion<string>().HasMaxLength(40);
        // One group per team per season: the pair test compares groups, so a team in two
        // of them would make "same group" ambiguous.
        builder.HasIndex(x => new { x.ConfigId, x.TeamId }).IsUnique();

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
