namespace BookStore.Application.Features.Books.Models;

internal sealed record BookCreatedAuditDetails(
    string Title,
    string Isbn,
    string Author,
    bool IsActive,
    int Quantity);
