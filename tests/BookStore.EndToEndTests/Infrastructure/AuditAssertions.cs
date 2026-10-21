using System.Text.Json;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.EndToEndTests.Infrastructure;

internal static class AuditAssertions
{
    internal static async Task<AuditEvent> SingleAsync(
        AppDbContext context, string entityName, Guid entityId, AuditAction action,
        Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        var events = await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.EntityName == entityName && audit.EntityId == entityId && audit.Action == action)
            .ToListAsync(cancellationToken);

        var audit = Assert.Single(events);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal(correlationId, audit.CorrelationId);
        Assert.NotEqual(default, audit.CreatedAt);
        Assert.Equal(TimeSpan.Zero, audit.CreatedAt.Offset);
        using var details = JsonDocument.Parse(audit.Details);
        Assert.Equal(JsonValueKind.Object, details.RootElement.ValueKind);
        Assert.NotEmpty(details.RootElement.EnumerateObject());
        return audit;
    }
}