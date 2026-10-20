using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;

using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Books.Queries;

public sealed record GetBooksQuery(
    int Page = 1,
    int PageSize = 20)
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

public sealed class GetBooksQueryHandler(IBookRepository bookRepository)
    : IRequestHandler<GetBooksQuery, PageResponse<BookResponse>>
{
    public async Task<PageResponse<BookResponse>> Handle(
        GetBooksQuery request,
        CancellationToken cancellationToken)
    {
        var booksPage = await bookRepository.GetPaginatedAsync(
            request.Page,
            request.PageSize,
            cancellationToken);

        if (booksPage.TotalCount == 0)
            return new PageResponse<BookResponse>([], request.Page, request.PageSize, booksPage.TotalCount);

        var items = booksPage.Items
            .Select(book => new BookResponse(
                book.Id,
                book.Title,
                book.Isbn,
                book.Author,
                book.Quantity,
                book.IsActive))
            .ToArray();

        return new PageResponse<BookResponse>(
            items,
            request.Page,
            request.PageSize,
            booksPage.TotalCount);
    }
}
