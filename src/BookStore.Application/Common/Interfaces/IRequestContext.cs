namespace BookStore.Application.Common.Interfaces;

public interface IRequestContext
{
    Guid ActorId { get; }
    string CorrelationId { get; }
}