using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Palpitao.Application.Abstractions;
using Palpitao.Domain.Common;
using Palpitao.Domain.Entities;

namespace Palpitao.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core (code-first) context: the Application's <see cref="IAppDbContext"/>,
/// plus the multi-tenant query filter and the insert-time stamping of group and public key.
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private static readonly PropertyInfo CurrentGroupIdProperty =
        typeof(AppDbContext).GetProperty(nameof(CurrentGroupId), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IRequestGroupContext? _groupContext;

    /// <param name="groupContext">
    /// Optional per-request group accessor. When supplied (HTTP requests), tenant-root
    /// entities are filtered to the current group and new rows are auto-stamped with it.
    /// When omitted (background services, seeding, design-time, unit tests) the filter and
    /// stamping are inert, so those paths still see/write across groups as before.
    /// </param>
    public AppDbContext(DbContextOptions<AppDbContext> options, IRequestGroupContext? groupContext = null)
        : base(options)
    {
        _groupContext = groupContext;
    }

    /// <summary>The current request's group, or null outside an HTTP request.</summary>
    private Guid? CurrentGroupId => _groupContext?.CurrentGroupId;

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
    public DbSet<FlavioOverride> FlavioOverrides => Set<FlavioOverride>();
    public DbSet<AbsenceOverride> AbsenceOverrides => Set<AbsenceOverride>();
    public DbSet<RoundParticipantResult> RoundParticipantResults => Set<RoundParticipantResult>();
    public DbSet<PredictionScore> PredictionScores => Set<PredictionScore>();
    public DbSet<SeasonScoringConfig> SeasonScoringConfigs => Set<SeasonScoringConfig>();
    public DbSet<ScoringScoreEntry> ScoringScoreEntries => Set<ScoringScoreEntry>();
    public DbSet<ScoringMultiplierRule> ScoringMultiplierRules => Set<ScoringMultiplierRule>();
    public DbSet<ScoringClassicTeam> ScoringClassicTeams => Set<ScoringClassicTeam>();
    public DbSet<OcrImportBatch> OcrImportBatches => Set<OcrImportBatch>();
    public DbSet<OcrImportImage> OcrImportImages => Set<OcrImportImage>();
    public DbSet<OcrPredictionCandidate> OcrPredictionCandidates => Set<OcrPredictionCandidate>();
    public DbSet<OcrParticipantAlias> OcrParticipantAliases => Set<OcrParticipantAlias>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // One IEntityTypeConfiguration per entity (Configurations/); seed rows live in Seed/.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ConfigureTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Defence-in-depth multi-tenant isolation: every read of a tenant-root entity is
    /// transparently scoped to the current request's group, so a query that forgets its
    /// explicit <c>GroupId</c> filter still cannot return another tenant's rows. Inert when
    /// there is no request group (background/seed/tests) — see <see cref="CurrentGroupId"/>.
    /// Implementing <see cref="IGroupOwned"/> is the whole opt-in.
    /// </summary>
    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var tenantRoots = modelBuilder.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(typeof(IGroupOwned).IsAssignableFrom)
            .ToList();

        foreach (var clrType in tenantRoots)
        {
            modelBuilder.Entity(clrType).HasQueryFilter(TenantFilter(clrType));
        }
    }

    /// <summary>
    /// <c>e =&gt; CurrentGroupId == null || e.GroupId == CurrentGroupId</c> for the given entity
    /// type. Built by hand because the lambda must bind the entity's own <c>GroupId</c>; it reads
    /// <see cref="CurrentGroupId"/> off this context instance, which EF parameterizes per query.
    /// </summary>
    private LambdaExpression TenantFilter(Type entityType)
    {
        var currentGroupId = Expression.Property(Expression.Constant(this), CurrentGroupIdProperty);
        var entity = Expression.Parameter(entityType, "e");
        var groupId = Expression.Convert(Expression.Property(entity, nameof(IGroupOwned.GroupId)), typeof(Guid?));
        return Expression.Lambda(
            Expression.OrElse(
                Expression.Equal(currentGroupId, Expression.Constant(null, typeof(Guid?))),
                Expression.Equal(groupId, currentGroupId)),
            entity);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // The tenant query filter lives on the roots (Season/Round/Standing/
        // RoundParticipantResult); their per-round children (Prediction, RoundMatch, …) are
        // always reached through a filtered root, so EF's required-navigation warning about
        // the asymmetry is expected here and would otherwise be noise.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampCurrentGroup();
        StampPublicKeys();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampCurrentGroup();
        StampPublicKeys();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stamps the current request's group on newly added tenant roots whose <c>GroupId</c>
    /// was left unset, so a forgotten assignment lands in the acting group instead of
    /// silently defaulting to the seeded default group. Inert outside an HTTP request, and
    /// never overwrites a group that was set explicitly (e.g. cross-group admin writes).
    /// </summary>
    private void StampCurrentGroup()
    {
        if (CurrentGroupId is not Guid groupId)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IGroupOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.GroupId == Guid.Empty)
            {
                entry.Entity.GroupId = groupId;
            }
        }
    }

    /// <summary>
    /// Mints a public key for newly added seasons that do not carry one. The key is unique
    /// and required, so an unset value is not merely incomplete -- the second such season
    /// would collide on the unique index. Stamping here makes the invariant hold for every
    /// write path (service, seeding, tests, future importers) instead of only the one that
    /// remembered. Never overwrites a key that was set explicitly.
    /// </summary>
    private void StampPublicKeys()
    {
        foreach (var entry in ChangeTracker.Entries<Season>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.PublicKey))
            {
                entry.Entity.PublicKey = PublicKeyGenerator.Generate();
            }
        }
    }
}
