using BookStore.Domain.Entities;

using BookStore.Application.Common.Models;

namespace BookStore.Application.Common.Interfaces;

public interface IAuditEventRepository : IBaseRepository<AuditEvent>
{
    Task<PagedResult<AuditEventResponse>> GetPaginatedAsync(
        int page,
        int pageSize,
        string? entityName,
        Guid? entityId,
        string? correlationId,
        CancellationToken cancellationToken);
}
