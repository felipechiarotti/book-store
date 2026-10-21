using System.Text.Json;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models.Auditing;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BookStore.Application.Features.Books.Commands;

public sealed record UpdateBookCommand(Guid Id, string? Title, string? Author, int? Quantity) : IRequest;

public sealed class UpdateBookCommandValidator : AbstractValidator<UpdateBookCommand>
{
    public UpdateBookCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("O identificador do livro é obrigatório.");

        RuleFor(command => command)
            .Must(command => command.Title is not null || command.Author is not null || command.Quantity.HasValue)
            .WithMessage("Informe ao menos um campo para atualizar.")
            .OverridePropertyName("Changes");

        RuleFor(command => command.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O título não pode ser vazio.")
            .Must(title => title is null || title.Trim().Length <= 200)
            .WithMessage("O título deve ter no máximo 200 caracteres.")
            .When(command => command.Title is not null);

        RuleFor(command => command.Author)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O autor não pode ser vazio.")
            .Must(author => author is null || author.Trim().Length <= 200)
            .WithMessage("O autor deve ter no máximo 200 caracteres.")
            .When(command => command.Author is not null);

        RuleFor(command => command.Quantity)
            .InclusiveBetween(0, 1000)
            .WithMessage("A quantidade deve estar entre 0 e 1000.")
            .When(command => command.Quantity.HasValue);
    }
}

public sealed class UpdateBookCommandHandler(
    IBookRepository bookRepository,
    ILoanRepository loanRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork,
    IRequestContext requestContext,
    IBookCatalogCache catalogCache) : IRequestHandler<UpdateBookCommand>
{
    public async Task Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var book = await bookRepository.GetByIdWithLockAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException("Livro não encontrado.");

            var before = new BookUpdateSnapshot(book.Title, book.Author, book.Quantity);
            var after = new BookUpdateSnapshot(
                request.Title?.Trim() ?? book.Title,
                request.Author?.Trim() ?? book.Author,
                request.Quantity ?? book.Quantity);

            if (before == after)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            if (after.Quantity != before.Quantity)
            {
                var activeLoans = await loanRepository.CountActiveByBookIdAsync(book.Id, cancellationToken);

                if (after.Quantity < activeLoans)
                    throw new ConflictException("A quantidade não pode ser menor que o número de empréstimos ativos.");
            }

            book.UpdateDetails(after.Title, after.Author, after.Quantity);

            var auditEvent = AuditEvent.Create(
                nameof(Book),
                book.Id,
                AuditAction.Updated,
                requestContext.ActorId,
                requestContext.CorrelationId,
                JsonSerializer.Serialize(new BookUpdatedAuditDetails(before, after)));

            auditEventRepository.Add(auditEvent);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await catalogCache.InvalidateAsync(CancellationToken.None);
    }
}