using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Loans.Queries;

public sealed record GetLoanByIdQuery(Guid Id) : IRequest<LoanResponse>;

public sealed class GetLoanByIdQueryValidator : AbstractValidator<GetLoanByIdQuery>
{
    public GetLoanByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("O identificador do empréstimo é obrigatório.");
    }
}

public sealed class GetLoanByIdQueryHandler(ILoanRepository loanRepository)
    : IRequestHandler<GetLoanByIdQuery, LoanResponse>
{
    public async Task<LoanResponse> Handle(
        GetLoanByIdQuery request,
        CancellationToken cancellationToken)
    {
        var loan = await loanRepository.GetByIdNotTrackedAsync(
                       request.Id,
                       cancellationToken)
                   ?? throw new NotFoundException("Empréstimo não encontrado.");

        return new LoanResponse(
            loan.Id,
            loan.BookId,
            loan.UserId,
            loan.LoanedAt,
            loan.DueAt,
            loan.Status,
            loan.ClosedAt);
    }
}