using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Abstraction over an external source of match results. Implementations are
/// isolated (no DB writes, no domain rules) and only return results for a round.
/// Swap the registration in <c>Program.cs</c> / config to change source.
/// </summary>
public interface IResultsProvider
{
    /// <summary>Short identifier of the source (e.g. "Manual", "ConfiguredWebsite").</summary>
    string Name { get; }

    /// <summary>False when no external source is configured/enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Returns the results known by the source for the round's matches.</summary>
    Task<IReadOnlyList<ExternalMatchResultDto>> GetResultsForRoundAsync(Round round, CancellationToken cancellationToken);
}
