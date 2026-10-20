using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Application.Common.Options;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace BookStore.Application.Features.Books.Queries;

public sealed record GetBookHistoryQuery(Guid Id, int Page = 1, int? PageSize = null)
    : IRequest<PageResponse<LoanHistoryResponse>>;

public sealed class GetBookHistoryQueryValidator : AbstractValidator<GetBookHistoryQuery>
{
    public GetBookHistoryQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("O identificador do livro é obrigatório.");

        RuleFor(query => query.Page)
            .GreaterThan(0)
            .WithMessage("A página deve ser maior que zero.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("O tamanho da página deve estar entre 1 e 100.");
    }
}

public sealed class GetBookHistoryQueryHandler(
    IBookRepository bookRepository,
    ILoanRepository loanRepository,
    IOptions<PaginationOptions> paginationOptions)
    : IRequestHandler<GetBookHistoryQuery, PageResponse<LoanHistoryResponse>>
{
    public async Task<PageResponse<LoanHistoryResponse>> Handle(
        GetBookHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize ?? paginationOptions.Value.DefaultPageSize;

        if (!await bookRepository.ExistsByIdAsync(request.Id, cancellationToken))
            throw new NotFoundException("Livro não encontrado.");

        var history = await loanRepository.GetHistoryByBookIdAsync(
            request.Id,
            request.Page,
            pageSize,
            cancellationToken);

        var items = history.Items
            .Select(entry => new LoanHistoryResponse(
                entry.Id,
                entry.BookId,
                entry.UserId,
                entry.LoanedAt,
                entry.DueAt,
                entry.ClosedAt,
                entry.Status.ToString()))
            .ToArray();

        return new PageResponse<LoanHistoryResponse>(
            items,
            request.Page,
            pageSize,
            history.TotalCount);
    }
}
