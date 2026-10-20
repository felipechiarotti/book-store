namespace BookStore.Application.Common.Models.Auditing;

internal sealed record BookUpdateSnapshot(string Title, string Author, int Quantity);

internal sealed record BookUpdatedAuditDetails(BookUpdateSnapshot Before, BookUpdateSnapshot After);
