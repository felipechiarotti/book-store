using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Loans.Commands;

public sealed record CancelLoanCommand(Guid Id) : IRequest;

public sealed class CancelLoanCommandValidator : AbstractValidator<CancelLoanCommand>
{
    public CancelLoanCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("O identificador do empréstimo é obrigatório.");
    }
}