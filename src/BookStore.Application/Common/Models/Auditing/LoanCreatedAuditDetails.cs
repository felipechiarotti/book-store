namespace BookStore.Application.Common.Models.Auditing;

internal sealed record LoanCreatedAuditDetails(
    Guid BookId,
    Guid UserId,
    DateTimeOffset LoanedAt,
    DateTimeOffset DueAt);