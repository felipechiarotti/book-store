using BookStore.Application.Common.Models;

namespace BookStore.Application.Common.Interfaces;

public interface IBookCatalogCache
{
    Task<string?> GetGenerationAsync(CancellationToken cancellationToken);
    Task<PageResponse<BookResponse>?> GetPageAsync(
        string generation, int page, int pageSize, CancellationToken cancellationToken);
    Task SetPageAsync(
        string generation, PageResponse<BookResponse> page, CancellationToken cancellationToken);
    Task InvalidateAsync(CancellationToken cancellationToken);
}
