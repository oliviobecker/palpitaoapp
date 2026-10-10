using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        // The certame type lives on the season (the certame instance); existing
        // seasons default to the England certame on migration.
        builder.Property(x => x.TournamentType).HasConversion<string>().HasMaxLength(40)
            .HasDefaultValue(TournamentType.PalpitaoEngland);
        // Prediction settings live on the season (the certame instance).
        builder.Property(x => x.AllowParticipantsToViewOthersPredictions).HasDefaultValue(false);
        builder.Property(x => x.AllowParticipantsToSubmitPredictions).HasDefaultValue(true);
        // FaCupEnabled declares no store default; the entity initializer carries it, and
        // the migration backfills existing rows. (A store default would be safe too: EF
        // takes the initializer as the property's sentinel, so turning the flag off on
        // creation is still written explicitly instead of being omitted from the INSERT.)
        // Public standings link: the key is the whole credential, so it is unique across
        // every group (a lookup by key has no tenant context to scope it with).
        builder.Property(x => x.PublicKey).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.PublicKey).IsUnique();
        builder.Property(x => x.PublicStandingsEnabled).HasDefaultValue(false);
        builder.HasGroupOwnership();
    }
}
