using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public class BookRepository(AppDbContext dbContext) : BaseRepository<Book>(dbContext), IBookRepository
{
    public Task<BookAvailability?> GetAvailabilityAsync(
        Guid id,
        CancellationToken cancellationToken)
        => DbSet
            .Where(book => book.Id == id)
            .Select(book => new BookAvailability(
                book.Quantity,
                DbContext.Loans.Count(loan =>
                    loan.BookId == book.Id && loan.Status == LoanStatus.Active),
                book.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

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

    public async Task<PagedResult<Book>> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await DbSet.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;

        if (offset >= totalCount)
            return new PagedResult<Book>([], totalCount);

        var items = await DbSet
            .AsNoTracking()
            .OrderBy(book => book.CreatedAt)
            .ThenBy(book => book.Title)
            .ThenBy(book => book.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Book>(items, totalCount);
    }

    public Task<Book?> GetByIdWithLockAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "A consulta com bloqueio exige uma transação ativa.");

        return DbSet
            .FromSql($"""
                      SELECT *
                      FROM books
                      WHERE "Id" = {id}
                      FOR UPDATE
                      """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
