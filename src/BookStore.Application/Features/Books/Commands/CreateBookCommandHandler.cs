using System.Text.Json;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models.Auditing;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.Domain.Rules;
using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Books.Commands;

public record CreateBookCommand(
    string Title,
    string Isbn,
    string Author,
    int Quantity) : IRequest<CreateBookResponse>;

public sealed record CreateBookResponse(Guid Id);

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
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork,
    IRequestContext requestContext,
    IBookCatalogCache catalogCache)
    : IRequestHandler<CreateBookCommand, CreateBookResponse>
{
    public async Task<CreateBookResponse> Handle(
        CreateBookCommand request,
        CancellationToken cancellationToken)
    {
        var actorId = requestContext.ActorId;
        var correlationId = requestContext.CorrelationId;

        var book = Book.Create(
            request.Title,
            request.Isbn,
            request.Author,
            request.Quantity);

        if (await bookRepository.ExistsByIsbnAsync(book.Isbn, cancellationToken))
            throw new ConflictException(
                "Já existe um livro cadastrado com este ISBN.");

        var details = new BookCreatedAuditDetails(book.Title, book.Isbn, book.Author, book.IsActive, book.Quantity);

        var auditEvent = AuditEvent.Create(
            nameof(Book),
            book.Id,
            AuditAction.Created,
            actorId,
            correlationId,
            JsonSerializer.Serialize(details));

        bookRepository.Add(book);
        auditEventRepository.Add(auditEvent);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await catalogCache.InvalidateAsync(CancellationToken.None);

        return new CreateBookResponse(book.Id);
    }
}