using System.Data;
using BookStore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Data;

public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        return new ApplicationTransaction(transaction);
    }
}