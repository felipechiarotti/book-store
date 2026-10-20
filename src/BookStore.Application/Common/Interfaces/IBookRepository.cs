using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface IBookRepository : IBaseRepository<Book>
{
    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Book?> GetByIdNotTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken);

    Task<Book?> GetByIdWithLockAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<(int TotalCount, IReadOnlyList<Book> Items)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

}
