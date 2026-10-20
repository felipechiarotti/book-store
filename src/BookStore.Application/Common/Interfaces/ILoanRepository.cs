using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface ILoanRepository : IBaseRepository<Loan>
{
    Task<Loan?> GetByIdNotTrackedAsync(Guid id, CancellationToken cancellationToken);

    Task<Loan?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<int> CountActiveByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken);
}
