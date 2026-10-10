using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class OcrPredictionCandidateConfiguration : IEntityTypeConfiguration<OcrPredictionCandidate>
{
    public void Configure(EntityTypeBuilder<OcrPredictionCandidate> builder)
    {
        builder.Property(x => x.ParticipantNameRaw)
            .HasMaxLength(OcrPredictionCandidate.MaxParticipantNameLength);
        builder.Property(x => x.MatchTextRaw).HasMaxLength(OcrPredictionCandidate.MaxMatchTextLength);
        builder.Property(x => x.ReviewNotes).HasMaxLength(OcrPredictionCandidate.MaxReviewNotesLength);
        builder.HasIndex(x => x.OcrImportBatchId);

        builder.HasOne(x => x.Batch)
            .WithMany(b => b.Candidates)
            .HasForeignKey(x => x.OcrImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
