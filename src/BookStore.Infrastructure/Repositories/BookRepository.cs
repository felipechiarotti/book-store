using BookStore.Application.Common.Interfaces;
using BookStore.Application.Features.Books.Models;
using BookStore.Domain.Entities;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public class BookRepository(AppDbContext dbContext) : BaseRepository<Book>(dbContext), IBookRepository
{
    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await DbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking().AnyAsync(b => b.Isbn == isbn, cancellationToken);
    }

    public async Task<Book?> GetByIdNotTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}