using BookStore.Domain.Enums;

namespace BookStore.Application.Common.Models;

public sealed record LoanResponse(
    Guid Id,
    Guid BookId,
    Guid UserId,
    DateTimeOffset LoanedAt,
    DateTimeOffset DueAt,
    LoanStatus Status,
    DateTimeOffset? ClosedAt);