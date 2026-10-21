using BookStore.Application.Features.Books.Diagnostics;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Application.Common.Options;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Books.Queries;

public sealed record GetBooksQuery(
    int Page = 1,
    int? PageSize = null)
    : IRequest<PageResponse<BookResponse>>;

public sealed class GetBooksQueryValidator
    : AbstractValidator<GetBooksQuery>
{
    public GetBooksQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0)
            .WithMessage("A página deve ser maior que zero.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("O tamanho da página deve estar entre 1 e 100.");
    }
}

public sealed class GetBooksQueryHandler(
    IBookRepository bookRepository,
    IOptions<PaginationOptions> paginationOptions,
    IBookCatalogCache catalogCache,
    ILogger<GetBooksQueryHandler> logger)
    : IRequestHandler<GetBooksQuery, PageResponse<BookResponse>>
{
    public async Task<PageResponse<BookResponse>> Handle(
        GetBooksQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize ?? paginationOptions.Value.DefaultPageSize;
        var generation = await catalogCache.GetGenerationAsync(cancellationToken);

        if (generation is not null)
        {
            var cachedPage = await catalogCache.GetPageAsync(
                generation, request.Page, pageSize, cancellationToken);
            if (cachedPage is not null)
            {
                logger.LogDebug(BookLogEvents.CatalogCacheRead,
                    "Catalog page {Page}, size {PageSize}: {CacheResult}.",
                    request.Page, pageSize, "Hit");
                return cachedPage;
            }
        }

        logger.LogDebug(BookLogEvents.CatalogCacheRead,
            "Catalog page {Page}, size {PageSize}: {CacheResult}.",
            request.Page, pageSize, generation is null ? "Bypassed" : "Miss");
        var booksPage = await bookRepository.GetPaginatedAsync(
            request.Page,
            pageSize,
            cancellationToken);

        var items = booksPage.Items
            .Select(book => new BookResponse(
                book.Id,
                book.Title,
                book.Isbn,
                book.Author,
                book.Quantity,
                book.IsActive))
            .ToArray();

        var response = new PageResponse<BookResponse>(
            items,
            request.Page,
            pageSize,
            booksPage.TotalCount);

        if (generation is not null)
            await catalogCache.SetPageAsync(generation, response, cancellationToken);

        return response;
    }
}
