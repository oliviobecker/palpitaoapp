using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ShortName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.CrestUrl).HasMaxLength(300);
        // Club (default) or national team (FIFA World Cup certames).
        builder.Property(x => x.TeamType).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(TeamType.Club);
        builder.Property(x => x.CountryCode).HasMaxLength(3);
        builder.Property(x => x.FifaCode).HasMaxLength(3);
        builder.Ignore(x => x.IsWorldChampion);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(TeamCatalogSeed.Clubs());
        builder.HasData(TeamCatalogSeed.NationalTeams());
    }
}
