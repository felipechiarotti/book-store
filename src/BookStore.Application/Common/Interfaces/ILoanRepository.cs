using BookStore.Application.Common.Models;
using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface ILoanRepository : IBaseRepository<Loan>
{
    Task<Guid?> GetBookIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Loan?> GetByIdWithLockAsync(Guid id, CancellationToken cancellationToken);

    Task<Loan?> GetByIdNotTrackedAsync(Guid id, CancellationToken cancellationToken);

    Task<Loan?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<PagedResult<LoanResponse>> GetHistoryByBookIdAsync(
        Guid bookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<int> CountActiveByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken);

    Task<PagedResult<LoanResponse>> GetHistoryByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}