namespace BookStore.Application.Common.Models;

public sealed record LoanHistoryResponse(
    Guid Id,
    Guid BookId,
    Guid UserId,
    DateTimeOffset LoanedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ClosedAt,
    string Status);
