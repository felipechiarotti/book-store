using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface IBookRepository : IBaseRepository<Book>
{
    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken);
}