using BookStore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace BookStore.Infrastructure.Data;

internal sealed class ApplicationTransaction(
    IDbContextTransaction transaction)
    : IApplicationTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        return transaction.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        return transaction.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return transaction.DisposeAsync();
    }
}