using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface IUserRepository : IBaseRepository<User>
{
    Task<bool> ExistsByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
