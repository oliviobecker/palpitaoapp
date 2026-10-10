using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> builder)
    {
        builder.Property(x => x.ScoreCategory).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(x => new { x.RoundMatchId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.RoundId, x.UserId });

        builder.HasOne(x => x.Round)
            .WithMany()
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RoundMatch)
            .WithMany(m => m.Predictions)
            .HasForeignKey(x => x.RoundMatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Predictions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
