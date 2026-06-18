using Palpitao.Api.DTOs.Results;
using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Results;

/// <summary>
/// Default results provider for when no external source is configured. It does not
/// fetch anything — results are whatever the admin entered manually (via the
/// "register results" screen). <see cref="IsEnabled"/> is false so the refresh
/// endpoint reports clearly that no external provider is active, while still
/// recomputing the temporary standings from the manual results already on the
/// matches.
/// </summary>
public class ManualResultsProvider : IResultsProvider
{
    public string Name => "Manual";

    public bool IsEnabled => false;

    public Task<IReadOnlyList<ExternalMatchResultDto>> GetResultsForRoundAsync(
        Round round, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ExternalMatchResultDto>>(Array.Empty<ExternalMatchResultDto>());
}
