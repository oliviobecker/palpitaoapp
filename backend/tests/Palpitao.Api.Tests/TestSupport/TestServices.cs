using Palpitao.Api.Data;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;

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
}
