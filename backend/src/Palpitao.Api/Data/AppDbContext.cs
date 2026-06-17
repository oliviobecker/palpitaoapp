using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Data;

/// <summary>
/// Entity Framework Core (code-first) database context for Palpitão England 2025/2026.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupUser> GroupUsers => Set<GroupUser>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<RoundMatch> RoundMatches => Set<RoundMatch>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Standing> Standings => Set<Standing>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<AbsenceOverride> AbsenceOverrides => Set<AbsenceOverride>();
    public DbSet<RoundParticipantResult> RoundParticipantResults => Set<RoundParticipantResult>();
    public DbSet<PredictionScore> PredictionScores => Set<PredictionScore>();
    public DbSet<OcrImportBatch> OcrImportBatches => Set<OcrImportBatch>();
    public DbSet<OcrPredictionCandidate> OcrPredictionCandidates => Set<OcrPredictionCandidate>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureModel(modelBuilder);
        SeedData(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private static void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Store all enums as readable strings rather than ints.
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            // Existing rows (migration) default to Approved so current accounts keep
            // logging in; public sign-ups set PendingApproval explicitly.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40)
                .HasDefaultValue(UserStatus.Approved);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Group>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            // Existing groups default to the England certame on migration.
            e.Property(x => x.TournamentType).HasConversion<string>().HasMaxLength(40)
                .HasDefaultValue(TournamentType.PalpitaoEngland);
            // Off by default: participants cannot see others' predictions unless the
            // admin opts in.
            e.Property(x => x.AllowParticipantsToViewOthersPredictions).HasDefaultValue(false);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<GroupUser>(e =>
        {
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            // Existing memberships default to active/not-eliminated on migration.
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.IsEliminated).HasDefaultValue(false);
            // A user has at most one membership per group.
            e.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.GroupId, x.Status });

            e.HasOne(x => x.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Season>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            ConfigureGroupOwnership<Season>(e);
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ShortName).HasMaxLength(60).IsRequired();
            e.Property(x => x.CrestUrl).HasMaxLength(300);
            // Club (default) or national team (FIFA World Cup certames).
            e.Property(x => x.TeamType).HasConversion<string>().HasMaxLength(30)
                .HasDefaultValue(TeamType.Club);
            e.Property(x => x.CountryCode).HasMaxLength(3);
            e.Property(x => x.FifaCode).HasMaxLength(3);
            e.Ignore(x => x.IsWorldChampion);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Round>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.HasIndex(x => new { x.SeasonId, x.Number }).IsUnique();
            ConfigureGroupOwnership<Round>(e);

            e.HasOne(x => x.Season)
                .WithMany(s => s.Rounds)
                .HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoundMatch>(e =>
        {
            e.Property(x => x.Competition).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Phase).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.ManualMultiplierJustification).HasMaxLength(500);
            // Non-zero enum default avoids EF's CLR-default sentinel overriding it.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30)
                .HasDefaultValue(MatchStatus.NotStarted);
            e.Property(x => x.ResultSource).HasMaxLength(40);
            e.Property(x => x.ExternalMatchId).HasMaxLength(120);
            e.Property(x => x.ExternalMatchUrl).HasMaxLength(400);

            e.HasOne(x => x.Round)
                .WithMany(r => r.Matches)
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.HomeTeam)
                .WithMany()
                .HasForeignKey(x => x.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AwayTeam)
                .WithMany()
                .HasForeignKey(x => x.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Prediction>(e =>
        {
            e.Property(x => x.ScoreCategory).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.RoundMatchId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.RoundId, x.UserId });

            e.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RoundMatch)
                .WithMany(m => m.Predictions)
                .HasForeignKey(x => x.RoundMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany(u => u.Predictions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Standing>(e =>
        {
            e.HasIndex(x => new { x.SeasonId, x.UserId }).IsUnique();
            ConfigureGroupOwnership<Standing>(e);

            e.HasOne(x => x.Season)
                .WithMany(s => s.Standings)
                .HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany(u => u.Standings)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Absence>(e =>
        {
            e.HasIndex(x => new { x.RoundId, x.UserId }).IsUnique();

            e.HasOne(x => x.Round)
                .WithMany(r => r.Absences)
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany(u => u.Absences)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AbsenceOverride>(e =>
        {
            e.Property(x => x.Justification).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.RoundId, x.UserId }).IsUnique();

            e.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoundParticipantResult>(e =>
        {
            e.HasIndex(x => new { x.RoundId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.SeasonId, x.UserId });
            ConfigureGroupOwnership<RoundParticipantResult>(e);

            e.HasOne(x => x.Season)
                .WithMany()
                .HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PredictionScore>(e =>
        {
            e.Property(x => x.ScoreCategory).HasConversion<string>().HasMaxLength(40);
            e.HasIndex(x => new { x.RoundMatchId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.RoundId, x.UserId });

            e.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.RoundMatch)
                .WithMany()
                .HasForeignKey(x => x.RoundMatchId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OcrImportBatch>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.OriginalFileName).HasMaxLength(300);
            e.Property(x => x.StoredFilePath).HasMaxLength(500);
            e.Property(x => x.LanguageUsed).HasMaxLength(20);

            e.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OcrPredictionCandidate>(e =>
        {
            e.Property(x => x.ParticipantNameRaw).HasMaxLength(200);
            e.Property(x => x.MatchTextRaw).HasMaxLength(300);
            e.Property(x => x.ReviewNotes).HasMaxLength(500);
            e.HasIndex(x => x.OcrImportBatchId);

            e.HasOne(x => x.Batch)
                .WithMany(b => b.Candidates)
                .HasForeignKey(x => x.OcrImportBatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(120).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(80);
            e.Property(x => x.Details).HasColumnType("jsonb");
            e.HasIndex(x => x.GroupId);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>
    /// Configures the tenant-ownership column shared by Season/Round/Standing/
    /// RoundParticipantResult: a required <c>GroupId</c> FK to <see cref="Group"/>
    /// that defaults to the seeded default group. The default value keeps existing
    /// rows (and tests that don't set a group) attached to the default group while
    /// still enforcing referential integrity.
    /// </summary>
    private static void ConfigureGroupOwnership<T>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e) where T : class
    {
        e.Property("GroupId").HasDefaultValue(SeedIds.DefaultGroup);
        e.HasIndex("GroupId");
        e.HasOne(typeof(Group), "Group")
            .WithMany()
            .HasForeignKey("GroupId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Stable timestamp so migrations stay deterministic.
        var seededAt = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        // --- Default group (tenant) -----------------------------------------
        // Owns all pre-existing data; the dev admin is its GroupAdmin. New groups
        // are created at runtime via /auth/create-group.
        modelBuilder.Entity<Group>().HasData(new Group
        {
            Id = SeedIds.DefaultGroup,
            Name = "Palpitão England 2025/2026",
            Slug = "palpitao-england-2025-2026",
            Description = "Bolão da temporada inglesa.",
            CreatedByUserId = SeedIds.AdminUser,
            OwnerUserId = SeedIds.AdminUser,
            IsActive = true,
            CreatedAt = seededAt,
            UpdatedAt = seededAt,
        });

        modelBuilder.Entity<GroupUser>().HasData(new GroupUser
        {
            Id = SeedIds.DefaultGroupAdminMembership,
            GroupId = SeedIds.DefaultGroup,
            UserId = SeedIds.AdminUser,
            Role = Enums.GroupRole.GroupAdmin,
            Status = Enums.GroupUserStatus.Approved,
            ApprovedAt = seededAt,
            CreatedAt = seededAt,
            UpdatedAt = seededAt,
        });

        // --- Club catalogue (season 2025/2026) ------------------------------
        // Full rosters of the three tracked league divisions. The "Big Seven"
        // keep their fixed seed ids (referenced by tests and the scoring rules);
        // every other club gets a deterministic id derived from its name so the
        // seed stays stable across migrations without hand-coding dozens of GUIDs.
        var bigSevenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Arsenal", "Chelsea", "Liverpool", "Manchester City",
            "Manchester United", "Newcastle", "Tottenham",
        };

        var clubs = new (Guid? Id, string Name, string Short, Competition Division)[]
        {
            // Premier League — Big Seven (fixed ids)
            (SeedIds.Arsenal, "Arsenal", "ARS", Competition.PremierLeague),
            (SeedIds.Chelsea, "Chelsea", "CHE", Competition.PremierLeague),
            (SeedIds.Liverpool, "Liverpool", "LIV", Competition.PremierLeague),
            (SeedIds.ManchesterCity, "Manchester City", "MCI", Competition.PremierLeague),
            (SeedIds.ManchesterUnited, "Manchester United", "MUN", Competition.PremierLeague),
            (SeedIds.Newcastle, "Newcastle", "NEW", Competition.PremierLeague),
            (SeedIds.Tottenham, "Tottenham", "TOT", Competition.PremierLeague),
            // Premier League — remaining clubs
            (null, "Aston Villa", "AVL", Competition.PremierLeague),
            (null, "Bournemouth", "BOU", Competition.PremierLeague),
            (null, "Brentford", "BRE", Competition.PremierLeague),
            (null, "Brighton & Hove Albion", "BHA", Competition.PremierLeague),
            (null, "Burnley", "BUR", Competition.PremierLeague),
            (null, "Crystal Palace", "CRY", Competition.PremierLeague),
            (null, "Everton", "EVE", Competition.PremierLeague),
            (null, "Fulham", "FUL", Competition.PremierLeague),
            (null, "Leeds United", "LEE", Competition.PremierLeague),
            (null, "Nottingham Forest", "NFO", Competition.PremierLeague),
            (null, "Sunderland", "SUN", Competition.PremierLeague),
            (null, "West Ham United", "WHU", Competition.PremierLeague),
            (null, "Wolverhampton Wanderers", "WOL", Competition.PremierLeague),
            // Championship
            (null, "Birmingham City", "BIR", Competition.Championship),
            (null, "Blackburn Rovers", "BLB", Competition.Championship),
            (null, "Bristol City", "BRC", Competition.Championship),
            (null, "Charlton Athletic", "CHA", Competition.Championship),
            (null, "Coventry City", "COV", Competition.Championship),
            (null, "Derby County", "DER", Competition.Championship),
            (null, "Hull City", "HUL", Competition.Championship),
            (null, "Ipswich Town", "IPS", Competition.Championship),
            (null, "Leicester City", "LEI", Competition.Championship),
            (null, "Middlesbrough", "MID", Competition.Championship),
            (null, "Millwall", "MIL", Competition.Championship),
            (null, "Norwich City", "NOR", Competition.Championship),
            (null, "Oxford United", "OXF", Competition.Championship),
            (null, "Portsmouth", "POR", Competition.Championship),
            (null, "Preston North End", "PNE", Competition.Championship),
            (null, "Queens Park Rangers", "QPR", Competition.Championship),
            (null, "Sheffield United", "SHU", Competition.Championship),
            (null, "Sheffield Wednesday", "SHW", Competition.Championship),
            (null, "Southampton", "SOU", Competition.Championship),
            (null, "Stoke City", "STK", Competition.Championship),
            (null, "Swansea City", "SWA", Competition.Championship),
            (null, "Watford", "WAT", Competition.Championship),
            (null, "West Bromwich Albion", "WBA", Competition.Championship),
            (null, "Wrexham", "WRE", Competition.Championship),
            // League One
            (null, "AFC Wimbledon", "WIM", Competition.LeagueOne),
            (null, "Barnsley", "BAR", Competition.LeagueOne),
            (null, "Blackpool", "BLA", Competition.LeagueOne),
            (null, "Bolton Wanderers", "BOL", Competition.LeagueOne),
            (null, "Bradford City", "BRA", Competition.LeagueOne),
            (null, "Burton Albion", "BTN", Competition.LeagueOne),
            (null, "Cardiff City", "CAR", Competition.LeagueOne),
            (null, "Doncaster Rovers", "DON", Competition.LeagueOne),
            (null, "Exeter City", "EXE", Competition.LeagueOne),
            (null, "Huddersfield Town", "HUD", Competition.LeagueOne),
            (null, "Leyton Orient", "LEY", Competition.LeagueOne),
            (null, "Lincoln City", "LIN", Competition.LeagueOne),
            (null, "Luton Town", "LUT", Competition.LeagueOne),
            (null, "Mansfield Town", "MNF", Competition.LeagueOne),
            (null, "Northampton Town", "NTH", Competition.LeagueOne),
            (null, "Peterborough United", "PET", Competition.LeagueOne),
            (null, "Plymouth Argyle", "PLY", Competition.LeagueOne),
            (null, "Port Vale", "PVL", Competition.LeagueOne),
            (null, "Reading", "REA", Competition.LeagueOne),
            (null, "Rotherham United", "ROT", Competition.LeagueOne),
            (null, "Stevenage", "STV", Competition.LeagueOne),
            (null, "Stockport County", "STO", Competition.LeagueOne),
            (null, "Wigan Athletic", "WIG", Competition.LeagueOne),
            (null, "Wycombe Wanderers", "WYC", Competition.LeagueOne),
        };

        modelBuilder.Entity<Team>().HasData(
            clubs.Select(c => new Team
            {
                Id = c.Id ?? DeterministicGuid(c.Name),
                Name = c.Name,
                ShortName = c.Short,
                IsBigSevenClub = bigSevenNames.Contains(c.Name),
                Division = c.Division,
                TeamType = TeamType.Club,
                CreatedAt = seededAt,
            }));

        // --- World champion national teams (FIFA World Cup certames) --------
        // A team with WorldCupTitles > 0 is a "campeã mundial", used by the World
        // Cup classic (double) multiplier in the knockout stage.
        var champions = new (string Name, string Fifa, string Iso, int Titles)[]
        {
            ("Brazil", "BRA", "BR", 5),
            ("Germany", "GER", "DE", 4),
            ("Argentina", "ARG", "AR", 3),
            ("France", "FRA", "FR", 2),
            ("Uruguay", "URU", "UY", 2),
            ("Spain", "ESP", "ES", 1),
            ("England", "ENG", "GB", 1),
        };

        modelBuilder.Entity<Team>().HasData(
            champions.Select(c => new Team
            {
                Id = DeterministicGuid("nation:" + c.Name),
                Name = c.Name,
                ShortName = c.Fifa,
                IsBigSevenClub = false,
                Division = null,
                TeamType = TeamType.NationalTeam,
                CountryCode = c.Iso,
                FifaCode = c.Fifa,
                WorldCupTitles = c.Titles,
                CreatedAt = seededAt,
            }));

        // --- Development admin user ------------------------------------------
        // Password: "Admin@123" (BCrypt). For local development only.
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = SeedIds.AdminUser,
            Name = "Administrador",
            Email = "admin@palpitao.local",
            PasswordHash = "$2a$11$rqqFHI1KeD4V96P8cdiPBeR8U8MEQEwED.AbOQ2aeuQeAeNJN3U.m",
            Role = UserRole.Admin,
            Status = UserStatus.Approved,
            IsActive = true,
            CreatedAt = seededAt,
        });
    }

    /// <summary>
    /// Derives a stable <see cref="Guid"/> from a club name so seeded clubs keep
    /// the same primary key across migrations without hand-coding GUID literals.
    /// Pure function of the (lower-cased) name, so the model snapshot stays
    /// deterministic.
    /// </summary>
    private static Guid DeterministicGuid(string name)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes("palpitao-team:" + name.ToLowerInvariant()));
        return new Guid(bytes);
    }
}
