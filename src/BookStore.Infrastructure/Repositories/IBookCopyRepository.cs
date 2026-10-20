using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using BookStore.Infrastructure.Data;

namespace BookStore.Infrastructure.Repositories;

public sealed class BookCopyRepository(AppDbContext context)
    : BaseRepository<BookCopy>(context), IBookCopyRepository
{
}