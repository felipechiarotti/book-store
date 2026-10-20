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
    public string RequestHash { get; private set; } = string.Empty;

    public static Loan Create(
        Guid bookId,
        Guid userId,
        DateTimeOffset loanedAt,
        DateTimeOffset dueAt,
        string idempotencyKey,
        string requestHash)
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
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash
        };
    }
}
