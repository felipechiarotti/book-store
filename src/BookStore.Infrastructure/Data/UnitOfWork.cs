using System.Data;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookStore.Infrastructure.Data;

public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public void ClearTracking()
        => context.ChangeTracker.Clear();

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_loans_IdempotencyKey"
            })
        {
            throw new IdempotencyKeyConflictException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            throw new ConflictException(
                "A operação conflita com um registro existente.",
                exception);
        }
    }

    public async Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        return new ApplicationTransaction(transaction);
    }
}
