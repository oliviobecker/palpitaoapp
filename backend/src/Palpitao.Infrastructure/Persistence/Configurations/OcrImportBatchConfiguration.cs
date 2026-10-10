using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class OcrImportBatchConfiguration : IEntityTypeConfiguration<OcrImportBatch>
{
    public void Configure(EntityTypeBuilder<OcrImportBatch> builder)
    {
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.OriginalFileName).HasMaxLength(OcrImportBatch.MaxOriginalFileNameLength);
        builder.Property(x => x.LanguageUsed).HasMaxLength(20);

        builder.HasOne(x => x.Round)
            .WithMany()
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
