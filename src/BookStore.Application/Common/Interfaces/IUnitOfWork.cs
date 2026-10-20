namespace BookStore.Application.Common.Interfaces;

public interface IUnitOfWork
{
    void ClearTracking();

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken);

    Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken);
}
