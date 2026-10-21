namespace BookStore.Application.Common.Models.Auditing;

internal sealed record BookCreatedAuditDetails(
    string Title,
    string Isbn,
    string Author,
    bool IsActive,
    int Quantity);