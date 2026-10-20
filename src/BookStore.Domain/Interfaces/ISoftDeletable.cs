namespace BookStore.Domain.Interfaces;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
}