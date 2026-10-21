using BookStore.Domain.Enums;

namespace BookStore.Application.Common.Models.Auditing;

internal sealed record LoanClosedAuditDetails(
    Guid BookId,
    Guid UserId,
    LoanStatus PreviousStatus,
    LoanStatus Status,
    DateTimeOffset ClosedAt);