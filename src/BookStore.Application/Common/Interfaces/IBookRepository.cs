using BookStore.Application.Common.Models;
using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface IBookRepository : IBaseRepository<Book>
{
    Task<BookAvailability?> GetAvailabilityAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Book?> GetByIdNotTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken);
    Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Book?> GetByIdWithLockAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<PagedResult<Book>> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

}
