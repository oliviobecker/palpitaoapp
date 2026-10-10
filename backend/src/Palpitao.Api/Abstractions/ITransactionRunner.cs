namespace Palpitao.Api.Abstractions;

/// <summary>
/// Runs a multi-step mutation atomically. Scoring, season replays and round regrouping span several
/// <c>SaveChanges</c>, so a failure mid-way must not leave a round or season half done. The work runs
/// in a serializable transaction, or joins the one already open when a caller composes operations;
/// on a non-relational provider it simply runs.
/// </summary>
public interface ITransactionRunner
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);

    Task InTransactionAsync(Func<Task> work, CancellationToken ct);
}
