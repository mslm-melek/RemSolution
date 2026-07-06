namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// A database transaction owned by a handler. Disposing without committing
/// rolls back.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
