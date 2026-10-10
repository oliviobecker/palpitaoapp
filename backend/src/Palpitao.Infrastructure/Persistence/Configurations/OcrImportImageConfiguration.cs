using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class OcrImportImageConfiguration : IEntityTypeConfiguration<OcrImportImage>
{
    public void Configure(EntityTypeBuilder<OcrImportImage> builder)
    {
        // Shared PK with the batch: no surrogate id, no extra index, and the bytes stay
        // unreachable unless a query names this table explicitly.
        builder.HasKey(x => x.OcrImportBatchId);
        builder.Property(x => x.OcrImportBatchId).ValueGeneratedNever();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FileExtension).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Batch)
            .WithOne(b => b.Image)
            .HasForeignKey<OcrImportImage>(x => x.OcrImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
