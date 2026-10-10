namespace Palpitao.Api.Abstractions;

/// <summary>
/// Lightweight, DB-free accessor for the group id carried on the current request's
/// <c>X-Group-Id</c> header. Unlike <c>ICurrentGroupService</c> it performs no validation and no
/// database access, so the persistence layer can consume it without a dependency cycle.
/// </summary>
/// <remarks>
/// Returns <c>null</c> when there is no HTTP context — background services, data seeding,
/// EF design-time and unit tests — which intentionally disables the multi-tenant query
/// filter and insert-stamping for those non-request paths. Access is still authorized
/// separately by <c>ICurrentGroupService</c>; this only scopes the data layer.
/// </remarks>
public interface IRequestGroupContext
{
    /// <summary>The current request's group id, or <c>null</c> outside an HTTP request.</summary>
    Guid? CurrentGroupId { get; }
}
