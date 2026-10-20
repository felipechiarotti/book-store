namespace BookStore.Domain.Entities;

public class BaseEntity
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
}