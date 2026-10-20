using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public sealed class UserRepository(AppDbContext context)
    : BaseRepository<User>(context), IUserRepository
{
    public Task<bool> ExistsByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
        => DbSet.AnyAsync(
            user => user.Id == id,
            cancellationToken);
}
