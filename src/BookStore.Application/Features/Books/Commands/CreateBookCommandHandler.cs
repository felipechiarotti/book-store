using System.Text.Json;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Features.Books.Models;
using BookStore.Domain.Entities;
using BookStore.Domain.Rules;
using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Books.Commands;

public record CreateBookCommand(
    string Title,
    string Isbn,
    string Author,
    int Quantity) : IRequest<Guid>;

public sealed class CreateBookCommandValidator
    : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty()
            .WithMessage("O título é obrigatório.")
            .MaximumLength(200)
            .WithMessage("O título deve ter no máximo 200 caracteres.");

        RuleFor(command => command.Author)
            .NotEmpty()
            .WithMessage("O autor é obrigatório.")
            .MaximumLength(200)
            .WithMessage("O autor deve ter no máximo 200 caracteres.");

        RuleFor(command => command.Isbn)
            .NotEmpty()
            .WithMessage("O ISBN é obrigatório.")
            .Must(IsbnRules.HasValidFormat)
            .WithMessage("Informe um ISBN-10 ou ISBN-13 em formato válido.");

        RuleFor(command => command.Quantity)
            .InclusiveBetween(0, 1000)
            .WithMessage("A quantidade deve estar entre 0 e 1000.");
    }
}

public sealed class CreateBookCommandHandler(
    IBookRepository bookRepository,
    IBookCopyRepository bookCopyRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork,
    IRequestContext requestContext)
    : IRequestHandler<CreateBookCommand, Guid>
{
    public async Task<Guid> Handle(
        CreateBookCommand request,
        CancellationToken cancellationToken)
    {
        var actorId = requestContext.ActorId;
        var correlationId = requestContext.CorrelationId;

        var book = Book.Create(
            request.Title,
            request.Isbn,
            request.Author);

        if (await bookRepository.ExistsByIsbnAsync(book.Isbn, cancellationToken))
            throw new ConflictException(
                "Já existe um livro cadastrado com este ISBN.");

        var bookCopies = CreateCopies(book.Id, request.Quantity);

        var details = new BookCreatedAuditDetails(book.Title, book.Isbn, book.Author, book.IsActive, request.Quantity);

        var auditEvent = AuditEvent.Create(
            nameof(Book),
            book.Id,
            "Created",
            actorId,
            correlationId,
            JsonSerializer.Serialize(details));

        bookRepository.Add(book);
        bookCopyRepository.Add(bookCopies);
        auditEventRepository.Add(auditEvent);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return book.Id;
    }

    private static IReadOnlyList<BookCopy> CreateCopies(Guid bookId, int quantity)
    {
        var copies = new List<BookCopy>(quantity);

        for (var index = 0; index < quantity; index++)
        {
            var inventoryCode = Guid.CreateVersion7().ToString("N");
            var copy = BookCopy.Create(bookId, inventoryCode);
            copies.Add(copy);
        }

        return copies;
    }
}