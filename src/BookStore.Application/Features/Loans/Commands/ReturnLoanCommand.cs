using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Loans.Commands;

public sealed record ReturnLoanCommand(Guid Id) : IRequest;

public sealed class ReturnLoanCommandValidator : AbstractValidator<ReturnLoanCommand>
{
    public ReturnLoanCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("O identificador do empréstimo é obrigatório.");
    }
}
