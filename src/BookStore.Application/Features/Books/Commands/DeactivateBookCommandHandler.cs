using BookStore.Application.Features.Books.Diagnostics;
using System.Text.Json;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models.Auditing;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Books.Commands;

public sealed record DeactivateBookCommand(Guid Id) : IRequest;

public sealed class DeactivateBookCommandValidator : AbstractValidator<DeactivateBookCommand>
{
    public DeactivateBookCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("O identificador do livro é obrigatório.");
    }
}

public sealed class DeactivateBookCommandHandler(
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork,
    IRequestContext requestContext,
    IBookCatalogCache catalogCache,
    ILogger<DeactivateBookCommandHandler> logger) : IRequestHandler<DeactivateBookCommand>
{
    public async Task Handle(DeactivateBookCommand request, CancellationToken cancellationToken)
    {
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var book = await bookRepository.GetByIdWithLockAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException("Livro não encontrado.");

            if (!book.IsActive)
            {
                await transaction.CommitAsync(cancellationToken);
                logger.LogDebug(BookLogEvents.Unchanged,
                    "Book {BookId} operation {Operation} made no change.",
                    book.Id, "Deactivate");
                return;
            }

            var previousIsActive = book.IsActive;
            book.Deactivate();

            var details = new BookDeactivatedAuditDetails(previousIsActive, book.IsActive);
            var auditEvent = AuditEvent.Create(
                nameof(Book),
                book.Id,
                AuditAction.Deactivated,
                requestContext.ActorId,
                requestContext.CorrelationId,
                JsonSerializer.Serialize(details));

            auditEventRepository.Add(auditEvent);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(BookLogEvents.Deactivated,
                "Book {BookId} deactivated by actor {ActorId}.",
                book.Id, requestContext.ActorId);
        }

        await catalogCache.InvalidateAsync(CancellationToken.None);
    }
}
