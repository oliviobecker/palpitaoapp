namespace Palpitao.Application.Abstractions;

/// <summary>What the readiness endpoint needs to know about the database: reachability and schema drift.</summary>
public interface IDatabaseHealthProbe
{
    /// <summary>True when the database answers. May throw on auth/host/DNS failures.</summary>
    Task<bool> CanConnectAsync(CancellationToken ct);

    /// <summary>Migrations recorded as applied (empty for a database created without migrations).</summary>
    Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken ct);

    /// <summary>Migrations the code knows that the database has not applied.</summary>
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct);
}
