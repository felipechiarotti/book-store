namespace BookStore.Domain.Entities;

public sealed class AuditEvent : BaseEntity
{
    private AuditEvent()
    {
    }

    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;

    public static AuditEvent Create(
        string entityName,
        Guid entityId,
        string action,
        Guid actorId,
        string correlationId,
        string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (entityId == Guid.Empty)
            throw new ArgumentException("EntityId inválido.", nameof(entityId));

        if (actorId == Guid.Empty)
            throw new ArgumentException("ActorId inválido.", nameof(actorId));

        return new AuditEvent
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ActorId = actorId,
            CorrelationId = correlationId,
            Details = details
        };
    }
}