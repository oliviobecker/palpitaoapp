using Palpitao.Api.Data;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Users;

namespace Palpitao.Api.Tests.TestSupport;

/// <summary>Factories for the collaborators most service unit tests need.</summary>
public static class TestServices
{
    /// <summary>
    /// The real <see cref="SeasonScoringConfigService"/>. Services that only read the
    /// season's rule params (<c>GetRuleParamsAsync</c>) get the defaults when the season
    /// has no persisted config, which is what most tests want; a test that customises the
    /// rules just inserts a <c>SeasonScoringConfig</c> row.
    /// </summary>
    public static SeasonScoringConfigService ScoringConfig(AppDbContext db, ICurrentGroupService? current = null)
        => new(db, new AuditService(db), current ?? new FakeCurrentGroupService());

    /// <summary>The real <see cref="AbsenceService"/> wired to the given (or default) group.</summary>
    public static AbsenceService Absences(AppDbContext db, ICurrentGroupService? current = null)
    {
        var group = current ?? new FakeCurrentGroupService();
        return new AbsenceService(db, new AuditService(db), group, ScoringConfig(db, group));
    }

    /// <summary>
    /// The real <see cref="UserAdminService"/>. It shares the <paramref name="db"/> with the
    /// <see cref="AbsenceService"/> it delegates to, mirroring the request-scoped wiring that
    /// makes activation + absence overrides commit in one transaction.
    /// </summary>
    public static UserAdminService UserAdmin(AppDbContext db, ICurrentGroupService? current = null)
    {
        var group = current ?? new FakeCurrentGroupService();
        return new UserAdminService(
            db, new AuditService(db), group, Absences(db, group), new FakeLocalizationService());
    }
}
