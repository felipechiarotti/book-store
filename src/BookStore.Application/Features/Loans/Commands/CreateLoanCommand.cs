using System.Text.Json;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Options;
using BookStore.Application.Common.Models.Auditing;
using BookStore.Application.Features.Loans.Diagnostics;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Loans.Commands;

public sealed record CreateLoanCommand(
    Guid BookId,
    Guid UserId,
    string IdempotencyKey)
    : IRequest<CreateLoanResponse>;

public sealed record CreateLoanResponse(
    Guid LoanId,
    Guid BookId,
    Guid UserId,
    DateTimeOffset LoanedAt,
    DateTimeOffset DueAt);

public sealed class CreateLoanCommandValidator
    : AbstractValidator<CreateLoanCommand>
{
    public CreateLoanCommandValidator()
    {
        RuleFor(command => command.BookId)
            .NotEmpty()
            .WithMessage("O identificador do livro é obrigatório.");

        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(command => command.IdempotencyKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("A chave de idempotência é obrigatória.")
            .MaximumLength(128)
            .WithMessage(
                "A chave de idempotência deve ter no máximo 128 caracteres.");
    }
}

public sealed class CreateLoanCommandHandler(
    IBookRepository bookRepository,
    IUserRepository userRepository,
    ILoanRepository loanRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork,
    IRequestContext requestContext,
    TimeProvider timeProvider,
    IOptions<LoanOptions> options,
    LoanMetrics metrics,
    ILogger<CreateLoanCommandHandler> logger)
    : IRequestHandler<CreateLoanCommand, CreateLoanResponse>
{
    public async Task<CreateLoanResponse> Handle(
        CreateLoanCommand request,
        CancellationToken cancellationToken)
    {
        var existingLoan = await loanRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey,
            cancellationToken);

        if (existingLoan is not null)
            return GetIdempotentResponse(existingLoan, request);

        try
        {
            return await CreateAsync(
                request,
                cancellationToken);
        }
        catch (IdempotencyKeyConflictException)
        {
            unitOfWork.ClearTracking();

            var confirmedLoan =
                await loanRepository.GetByIdempotencyKeyAsync(
                    request.IdempotencyKey,
                    cancellationToken);

            if (confirmedLoan is null)
                throw;

            return GetIdempotentResponse(confirmedLoan, request);
        }
    }

    private async Task<CreateLoanResponse> CreateAsync(
        CreateLoanCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var book = await bookRepository.GetByIdWithLockAsync(
            request.BookId,
            cancellationToken);

        var existingLoan = await loanRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey,
            cancellationToken);

        if (existingLoan is not null)
            return GetIdempotentResponse(existingLoan, request);

        if (book is null)
            throw new NotFoundException("Livro não encontrado.");

        if (!book.IsActive)
        {
            logger.LogInformation(LoanLogEvents.Rejected,
                "Loan request rejected for book {BookId}, user {UserId}, actor {ActorId}: {Reason}.",
                book.Id, request.UserId, requestContext.ActorId, "BookInactive");
            throw new ConflictException("O livro está desativado.");
        }

        var userExists = await userRepository.ExistsByIdAsync(
            request.UserId,
            cancellationToken);

        if (!userExists)
            throw new NotFoundException("Usuário não encontrado.");

        var activeLoans = await loanRepository.CountActiveByBookIdAsync(
            book.Id,
            cancellationToken);

        if (activeLoans >= book.Quantity)
        {
            metrics.RecordUnavailableRejection();
            logger.LogInformation(LoanLogEvents.Unavailable,
                "Loan rejected for book {BookId}, user {UserId}, actor {ActorId}: {Reason}; capacity {Quantity}, active loans {ActiveLoans}.",
                book.Id, request.UserId, requestContext.ActorId,
                "NoAvailableQuantity",
                book.Quantity, activeLoans);
            throw new ConflictException(
                "Não há exemplares disponíveis para empréstimo.");
        }

        var loanedAt = timeProvider.GetUtcNow();
        var dueAt = loanedAt.AddDays(options.Value.DurationDays);

        var loan = Loan.Create(
            book.Id,
            request.UserId,
            loanedAt,
            dueAt,
            request.IdempotencyKey);

        var details = new LoanCreatedAuditDetails(
            loan.BookId,
            loan.UserId,
            loan.LoanedAt,
            loan.DueAt);

        var auditEvent = AuditEvent.Create(
            nameof(Loan),
            loan.Id,
            AuditAction.Created,
            requestContext.ActorId,
            requestContext.CorrelationId,
            JsonSerializer.Serialize(details));

        loanRepository.Add(loan);
        auditEventRepository.Add(auditEvent);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        metrics.RecordCreated();
        logger.LogInformation(LoanLogEvents.Created,
            "Loan {LoanId} created for book {BookId}, user {UserId}, by actor {ActorId}.",
            loan.Id, loan.BookId, loan.UserId, requestContext.ActorId);

        return ToResponse(loan);
    }

    private CreateLoanResponse GetIdempotentResponse(
        Loan loan,
        CreateLoanCommand request)
    {
        if (loan.BookId != request.BookId || loan.UserId != request.UserId)
        {
            logger.LogInformation(LoanLogEvents.Rejected,
                "Loan request rejected for book {BookId}, user {UserId}, actor {ActorId}: {Reason}.",
                request.BookId, request.UserId, requestContext.ActorId, "IdempotencyKeyDataMismatch");
            throw new ConflictException(
                "A chave de idempotência já foi utilizada com outros dados de empréstimo.");
        }

        metrics.RecordIdempotentReplay();
        logger.LogInformation(LoanLogEvents.Replayed,
            "Loan {LoanId} replayed for book {BookId}, user {UserId}, by actor {ActorId}.",
            loan.Id, loan.BookId, loan.UserId, requestContext.ActorId);

        return ToResponse(loan);
    }

    private static CreateLoanResponse ToResponse(Loan loan)
    {
        return new CreateLoanResponse(
            loan.Id,
            loan.BookId,
            loan.UserId,
            loan.LoanedAt,
            loan.DueAt);
    }
}
