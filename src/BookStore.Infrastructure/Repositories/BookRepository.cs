using BookStore.Application.Common.Interfaces;
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
    
    public async Task<Book?> GetByIdNotTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking().AnyAsync(b => b.Isbn == isbn, cancellationToken);
    }

    public async Task<(int TotalCount, IReadOnlyList<Book> Items)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await DbSet.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;

        if (offset >= totalCount)
            return (totalCount, []);

        var items = await DbSet
            .AsNoTracking()
            .OrderBy(book => book.CreatedAt)
            .ThenBy(book => book.Title)
            .ThenBy(book => book.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return (totalCount, items);
    }
}