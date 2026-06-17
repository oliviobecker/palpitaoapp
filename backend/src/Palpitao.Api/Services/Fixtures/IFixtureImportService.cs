using Palpitao.Api.DTOs.Fixtures;

namespace Palpitao.Api.Services.Fixtures;

public interface IFixtureImportService
{
    /// <summary>Searches external fixtures for the period and enriches them with
    /// the suggested multiplier, Big Seven flag and "already in round" status.</summary>
    Task<SearchFixturesResponse> SearchAsync(SearchFixturesRequest request, Guid actingUserId, CancellationToken ct);

    /// <summary>Imports the selected fixtures as <c>RoundMatch</c> rows, creating
    /// missing teams and skipping duplicates.</summary>
    Task<ImportFixturesResponse> ImportAsync(Guid roundId, ImportFixturesRequest request, Guid actingUserId, CancellationToken ct);
}
