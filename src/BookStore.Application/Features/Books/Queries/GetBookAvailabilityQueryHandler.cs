using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Books.Queries;

public sealed record GetBookAvailabilityQuery(Guid Id)
    : IRequest<BookAvailabilityResponse>;

public sealed class GetBookAvailabilityQueryValidator
    : AbstractValidator<GetBookAvailabilityQuery>
{
    public GetBookAvailabilityQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("O identificador do livro é obrigatório.");
    }
}

public sealed class GetBookAvailabilityQueryHandler(IBookRepository bookRepository)
    : IRequestHandler<GetBookAvailabilityQuery, BookAvailabilityResponse>
{
    public async Task<BookAvailabilityResponse> Handle(
        GetBookAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        var availability =
            await bookRepository.GetAvailabilityAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Livro não encontrado.");

        var availableQuantity = availability.IsActive
            ? availability.Quantity - availability.ActiveLoans
            : 0;

        return new BookAvailabilityResponse(
            request.Id,
            availability.Quantity,
            availability.ActiveLoans,
            availableQuantity,
            availability.IsActive);
    }
}
