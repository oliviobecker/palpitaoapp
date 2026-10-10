using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class OcrParticipantAliasConfiguration : IEntityTypeConfiguration<OcrParticipantAlias>
{
    public void Configure(EntityTypeBuilder<OcrParticipantAlias> builder)
    {
        builder.Property(x => x.Alias).HasMaxLength(OcrParticipantAlias.MaxAliasLength).IsRequired();
        builder.Property(x => x.AliasRaw).HasMaxLength(OcrParticipantAlias.MaxAliasLength).IsRequired();
        // One meaning per alias per group: re-confirming the same name against a different
        // participant updates the row instead of leaving two answers to the same question.
        builder.HasIndex(x => new { x.GroupId, x.Alias }).IsUnique();
        builder.HasGroupOwnership();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
