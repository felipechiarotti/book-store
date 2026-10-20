using BookStore.Domain.Entities;

namespace BookStore.Application.Common.Interfaces;

public interface IAuditEventRepository : IBaseRepository<AuditEvent>
{
}