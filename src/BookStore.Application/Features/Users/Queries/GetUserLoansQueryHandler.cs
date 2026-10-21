using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Application.Common.Options;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace BookStore.Application.Features.Users.Queries;

public sealed record GetUserLoansQuery(Guid Id, int Page = 1, int? PageSize = null)
    : IRequest<PageResponse<LoanHistoryResponse>>;

public sealed class GetUserLoansQueryValidator : AbstractValidator<GetUserLoansQuery>
{
    public GetUserLoansQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(query => query.Page)
            .GreaterThan(0)
            .WithMessage("A página deve ser maior que zero.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("O tamanho da página deve estar entre 1 e 100.");
    }
}

public sealed class GetUserLoansQueryHandler(
    IUserRepository userRepository,
    ILoanRepository loanRepository,
    IOptions<PaginationOptions> paginationOptions)
    : IRequestHandler<GetUserLoansQuery, PageResponse<LoanHistoryResponse>>
{
    public async Task<PageResponse<LoanHistoryResponse>> Handle(
        GetUserLoansQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize ?? paginationOptions.Value.DefaultPageSize;

        if (!await userRepository.ExistsByIdAsync(request.Id, cancellationToken))
            throw new NotFoundException("Usuário não encontrado.");

        var history = await loanRepository.GetHistoryByUserIdAsync(
            request.Id,
            request.Page,
            pageSize,
            cancellationToken);

        var items = history.Items
            .Select(loan => new LoanHistoryResponse(
                loan.Id,
                loan.BookId,
                loan.UserId,
                loan.LoanedAt,
                loan.DueAt,
                loan.ClosedAt,
                loan.Status.ToString()))
            .ToArray();

        return new PageResponse<LoanHistoryResponse>(
            items,
            request.Page,
            pageSize,
            history.TotalCount);
    }
}