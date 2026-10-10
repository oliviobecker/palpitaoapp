using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Palpitao.Api.Entities;

namespace Palpitao.Api.Abstractions;

/// <summary>
/// The persistence port the use cases work against: the model's sets and the unit of work. EF
/// Core's <see cref="DbSet{TEntity}"/> is the repository abstraction — wrapping it in generic
/// repositories would hide the LINQ the services rely on without isolating anything. Tenant
/// scoping and insert stamping are applied by the implementation, not by callers.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupUser> GroupUsers { get; }
    DbSet<Season> Seasons { get; }
    DbSet<Team> Teams { get; }
    DbSet<Round> Rounds { get; }
    DbSet<RoundMatch> RoundMatches { get; }
    DbSet<Prediction> Predictions { get; }
    DbSet<Standing> Standings { get; }
    DbSet<Absence> Absences { get; }
    DbSet<FlavioOverride> FlavioOverrides { get; }
    DbSet<AbsenceOverride> AbsenceOverrides { get; }
    DbSet<RoundParticipantResult> RoundParticipantResults { get; }
    DbSet<PredictionScore> PredictionScores { get; }
    DbSet<SeasonScoringConfig> SeasonScoringConfigs { get; }
    DbSet<ScoringScoreEntry> ScoringScoreEntries { get; }
    DbSet<ScoringMultiplierRule> ScoringMultiplierRules { get; }
    DbSet<ScoringClassicTeam> ScoringClassicTeams { get; }
    DbSet<OcrImportBatch> OcrImportBatches { get; }
    DbSet<OcrImportImage> OcrImportImages { get; }
    DbSet<OcrPredictionCandidate> OcrPredictionCandidates { get; }
    DbSet<OcrParticipantAlias> OcrParticipantAliases { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
