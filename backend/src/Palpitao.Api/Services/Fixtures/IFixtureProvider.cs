using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Fixtures;

/// <summary>
/// Abstraction over an external source of football fixtures. Implementations are
/// fully isolated (no DB access, no domain logic) and only return normalized
/// candidates for the requested period and competitions. Swap the registration in
/// <c>Program.cs</c> to change provider (OneFootball, API-Football, etc.).
/// </summary>
public interface IFixtureProvider
{
    /// <summary>A short identifier of the source (e.g. "OneFootball").</summary>
    string SourceName { get; }

    /// <summary>
    /// Returns fixtures available in the period for the given competitions.
    /// Throws <see cref="Common.BusinessRuleException"/> with key
    /// <c>fixtures.fetchFailed</c> when the source cannot be reached or parsed.
    /// </summary>
    Task<IReadOnlyList<FixtureCandidateDto>> SearchFixturesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Competition> competitions,
        CancellationToken cancellationToken);
}
