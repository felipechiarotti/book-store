using BookStore.Application.Common.Interfaces;
using BookStore.Application.Features.Books.Models;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
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

    public Task<BookResponse?> GetDetailsByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
        => DbSet
            .AsNoTracking()
            .Where(book => book.Id == id)
            .Select(book => new BookResponse(
                book.Id,
                book.Title,
                book.Isbn,
                book.Author,
                book.IsActive,
                book.Copies.Count()))
            .SingleOrDefaultAsync(cancellationToken);
}