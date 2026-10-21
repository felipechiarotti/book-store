using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public sealed class LoanRepository(AppDbContext context)
    : BaseRepository<Loan>(context), ILoanRepository
{
    public Task<Guid?> GetBookIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return DbSet
            .Where(loan => loan.Id == id)
            .Select(loan => (Guid?)loan.BookId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Loan?> GetByIdWithLockAsync(Guid id, CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "A consulta com bloqueio exige uma transação ativa.");

        return DbSet
            .FromSql($"""
                      SELECT *
                      FROM "loans"
                      WHERE "Id" = {id}
                      FOR UPDATE
                      """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Loan?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(
                loan => loan.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public Task<Loan?> GetByIdNotTrackedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return DbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);
    }

    public async Task<int> CountActiveByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken)
    {
        return await DbSet.CountAsync(
            loan => loan.BookId == bookId
                    && loan.Status == LoanStatus.Active,
            cancellationToken);
    }

    public Task<PagedResult<LoanResponse>> GetHistoryByBookIdAsync(
        Guid bookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return GetHistoryPageAsync(
            DbSet.Where(loan => loan.BookId == bookId),
            page,
            pageSize,
            cancellationToken);
    }

    public Task<PagedResult<LoanResponse>> GetHistoryByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return GetHistoryPageAsync(
            DbSet.Where(loan => loan.UserId == userId),
            page,
            pageSize,
            cancellationToken);
    }

    private static async Task<PagedResult<LoanResponse>> GetHistoryPageAsync(
        IQueryable<Loan> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;

        if (offset >= totalCount)
            return new PagedResult<LoanResponse>([], totalCount);

        var items = await query
            .AsNoTracking()
            .OrderByDescending(loan => loan.LoanedAt)
            .ThenByDescending(loan => loan.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(loan => new LoanResponse(
                loan.Id,
                loan.BookId,
                loan.UserId,
                loan.LoanedAt,
                loan.DueAt,
                loan.Status,
                loan.ClosedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<LoanResponse>(items, totalCount);
    }
}