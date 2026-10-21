using BookStore.Domain.Enums;

namespace BookStore.Application.Common.Models;

public sealed record AuditEventResponse(
    Guid Id,
    string EntityName,
    Guid EntityId,
    AuditAction Action,
    Guid ActorId,
    DateTimeOffset CreatedAt,
    string CorrelationId,
    string Details);