using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class RoundMatchConfiguration : IEntityTypeConfiguration<RoundMatch>
{
    public void Configure(EntityTypeBuilder<RoundMatch> builder)
    {
        builder.Property(x => x.Competition).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Phase).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ManualMultiplierJustification).HasMaxLength(500);
        // Non-zero enum default avoids EF's CLR-default sentinel overriding it.
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(MatchStatus.NotStarted);
        builder.Property(x => x.ResultSource).HasMaxLength(40);
        builder.Property(x => x.ExternalMatchId).HasMaxLength(120);
        builder.Property(x => x.ExternalMatchUrl).HasMaxLength(400);

        builder.HasOne(x => x.Round)
            .WithMany(r => r.Matches)
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.HomeTeam)
            .WithMany()
            .HasForeignKey(x => x.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AwayTeam)
            .WithMany()
            .HasForeignKey(x => x.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
