using BookStore.Application.Common.Interfaces;
using BookStore.Domain.Entities;
using BookStore.Infrastructure.Data;

namespace BookStore.Infrastructure.Repositories;

public sealed class AuditEventRepository(AppDbContext context)
    : BaseRepository<AuditEvent>(context), IAuditEventRepository
{
}