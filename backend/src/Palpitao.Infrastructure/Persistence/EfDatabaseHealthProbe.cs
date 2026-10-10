using Microsoft.EntityFrameworkCore;
using Palpitao.Application.Abstractions;

namespace Palpitao.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class EfDatabaseHealthProbe(AppDbContext db) : IDatabaseHealthProbe
{
    public Task<bool> CanConnectAsync(CancellationToken ct) => db.Database.CanConnectAsync(ct);

    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken ct)
        => (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct)
        => (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
}
