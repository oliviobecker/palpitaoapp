using System.Data;
using Microsoft.EntityFrameworkCore;
using Palpitao.Application.Abstractions;

namespace Palpitao.Api.Data;

/// <inheritdoc />
/// <remarks>
/// Scoped with the request's <see cref="AppDbContext"/>: the transaction must belong to the same
/// context instance the services write through.
/// </remarks>
public sealed class EfTransactionRunner(AppDbContext db) : ITransactionRunner
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await work();
        await tx.CommitAsync(ct);
        return result;
    }

    public Task InTransactionAsync(Func<Task> work, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await work();
            return true;
        }, ct);
}
