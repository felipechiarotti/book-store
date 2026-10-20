using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Features.Books.Models;
using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Books.Queries;

public sealed record GetBookByIdQuery(Guid Id)
    : IRequest<BookResponse>;

public sealed class GetBookByIdQueryValidator
    : AbstractValidator<GetBookByIdQuery>
{
    public GetBookByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("O identificador do livro é obrigatório.");
    }
}

public sealed class GetBookByIdQueryHandler(IBookRepository bookRepository)
    : IRequestHandler<GetBookByIdQuery, BookResponse>
{
    public async Task<BookResponse> Handle(
        GetBookByIdQuery request,
        CancellationToken cancellationToken)
    {
        var bookEntity = await bookRepository.GetByIdNotTrackedAsync(
                   request.Id,
                   cancellationToken)
               ?? throw new NotFoundException("Livro não encontrado.");
        
        return new BookResponse(bookEntity.Id, bookEntity.Title, bookEntity.Isbn, bookEntity.Author, bookEntity.Quantity, bookEntity.IsActive);
    }
}