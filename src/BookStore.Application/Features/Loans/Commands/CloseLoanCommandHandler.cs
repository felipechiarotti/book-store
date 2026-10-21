using BookStore.Application.Features.Loans.Diagnostics;
using System.Text.Json;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models.Auditing;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Loans.Commands;

public sealed class CloseLoanCommandHandler(
    IBookRepository bookRepository,
    ILoanRepository loanRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork,
    IRequestContext requestContext,
    TimeProvider timeProvider,
    ILogger<CloseLoanCommandHandler> logger)
    : IRequestHandler<ReturnLoanCommand>, IRequestHandler<CancelLoanCommand>
{
    public Task Handle(ReturnLoanCommand request, CancellationToken cancellationToken)
    {
        return CloseAsync(request.Id, LoanStatus.Returned, cancellationToken);
    }

    public Task Handle(CancelLoanCommand request, CancellationToken cancellationToken)
    {
        return CloseAsync(request.Id, LoanStatus.Cancelled, cancellationToken);
    }

    private async Task CloseAsync(
        Guid id,
        LoanStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var bookId = await loanRepository.GetBookIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Empréstimo não encontrado.");

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var book = await bookRepository.GetByIdWithLockAsync(bookId, cancellationToken)
                   ?? throw new NotFoundException("Livro não encontrado.");

        var loan = await loanRepository.GetByIdWithLockAsync(id, cancellationToken)
                   ?? throw new NotFoundException("Empréstimo não encontrado.");

        if (loan.Status == targetStatus)
        {
            await transaction.CommitAsync(cancellationToken);
            logger.LogDebug(LoanLogEvents.Unchanged,
                "Loan {LoanId} already has status {Status}; no transition requested by actor {ActorId}.",
                loan.Id, loan.Status, requestContext.ActorId);
            return;
        }

        if (loan.Status != LoanStatus.Active)
        {
            logger.LogInformation(LoanLogEvents.ClosureRejected,
                "Loan {LoanId} closure rejected: {Reason}; current {Status}, requested {RequestedStatus}, actor {ActorId}.",
                loan.Id, "LoanAlreadyClosed", loan.Status, targetStatus, requestContext.ActorId);
            throw new ConflictException("O empréstimo já foi encerrado com outra ação.");
        }

        var previousStatus = loan.Status;
        var closedAt = timeProvider.GetUtcNow();
        AuditAction action;

        switch (targetStatus)
        {
            case LoanStatus.Returned:
                loan.Return(closedAt);
                action = AuditAction.Returned;
                break;
            case LoanStatus.Cancelled:
                loan.Cancel(closedAt);
                action = AuditAction.Cancelled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus));
        }

        var details = new LoanClosedAuditDetails(
            book.Id,
            loan.UserId,
            previousStatus,
            loan.Status,
            closedAt);

        var auditEvent = AuditEvent.Create(
            nameof(Loan),
            loan.Id,
            action,
            requestContext.ActorId,
            requestContext.CorrelationId,
            JsonSerializer.Serialize(details));

        auditEventRepository.Add(auditEvent);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(LoanLogEvents.Closed,
            "Loan {LoanId} for book {BookId}, user {UserId}, changed from {PreviousStatus} to {Status} by actor {ActorId}.",
            loan.Id, loan.BookId, loan.UserId, previousStatus, loan.Status, requestContext.ActorId);
    }
}
