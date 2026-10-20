using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using BookStore.Infrastructure.Data;

using BookStore.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Repositories;

public sealed class AuditEventRepository(AppDbContext context)
    : BaseRepository<AuditEvent>(context), IAuditEventRepository
{
    public async Task<PagedResult<AuditEventResponse>> GetPaginatedAsync(
        int page,
        int pageSize,
        string? entityName,
        Guid? entityId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var query = DbSet.AsNoTracking();

        if (entityName is not null)
            query = query.Where(audit => audit.EntityName == entityName);

        if (entityId.HasValue)
            query = query.Where(audit => audit.EntityId == entityId.Value);

        if (correlationId is not null)
            query = query.Where(audit => audit.CorrelationId == correlationId);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;

        if (offset >= totalCount)
            return new PagedResult<AuditEventResponse>([], totalCount);

        var items = await query
            .OrderByDescending(audit => audit.CreatedAt)
            .ThenByDescending(audit => audit.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(audit => new AuditEventResponse(
                audit.Id,
                audit.EntityName,
                audit.EntityId,
                audit.Action,
                audit.ActorId,
                audit.CreatedAt,
                audit.CorrelationId,
                audit.Details))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEventResponse>(items, totalCount);
    }
}
