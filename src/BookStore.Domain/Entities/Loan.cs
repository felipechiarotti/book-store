using BookStore.Domain.Enums;

namespace BookStore.Domain.Entities;

public sealed class Loan : BaseEntity
{
    private Loan()
    {
    }

    public Guid BookId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset LoanedAt { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public LoanStatus Status { get; private set; } = LoanStatus.Active;
    public string IdempotencyKey { get; private set; } = string.Empty;

    public void Return(DateTimeOffset returnedAt)
    {
        Close(LoanStatus.Returned, returnedAt);
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        Close(LoanStatus.Cancelled, cancelledAt);
    }

    private void Close(LoanStatus status, DateTimeOffset closedAt)
    {
        if (Status != LoanStatus.Active)
            throw new InvalidOperationException("O empréstimo já foi encerrado.");

        if (closedAt < LoanedAt)
            throw new ArgumentOutOfRangeException(
                nameof(closedAt),
                "A data de encerramento não pode ser anterior à data do empréstimo.");

        Status = status;
        ClosedAt = closedAt.ToUniversalTime();
    }

    public static Loan Create(
        Guid bookId,
        Guid userId,
        DateTimeOffset loanedAt,
        DateTimeOffset dueAt,
        string idempotencyKey)
    {
        if (dueAt <= loanedAt)
            throw new ArgumentOutOfRangeException(
                nameof(dueAt),
                "A data prevista de devolução deve ser posterior à data do empréstimo.");

        return new Loan
        {
            BookId = bookId,
            UserId = userId,
            LoanedAt = loanedAt.ToUniversalTime(),
            DueAt = dueAt.ToUniversalTime(),
            IdempotencyKey = idempotencyKey
        };
    }
}