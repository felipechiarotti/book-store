using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public sealed class LoanRepository(AppDbContext context)
    : BaseRepository<Loan>(context), ILoanRepository
{
    public Task<Loan?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(
                loan => loan.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<Loan?> GetByIdNotTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
        => DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public async Task<int> CountActiveByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken)
        => await DbSet.CountAsync(
            loan => loan.BookId == bookId
                    && loan.Status == LoanStatus.Active,
            cancellationToken);
}
