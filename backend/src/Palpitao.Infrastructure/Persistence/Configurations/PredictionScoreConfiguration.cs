using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class PredictionScoreConfiguration : IEntityTypeConfiguration<PredictionScore>
{
    public void Configure(EntityTypeBuilder<PredictionScore> builder)
    {
        builder.Property(x => x.ScoreCategory).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => new { x.RoundMatchId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.RoundId, x.UserId });

        builder.HasOne(x => x.Round)
            .WithMany()
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RoundMatch)
            .WithMany()
            .HasForeignKey(x => x.RoundMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
