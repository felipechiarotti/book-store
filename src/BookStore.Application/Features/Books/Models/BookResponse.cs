namespace BookStore.Application.Features.Books.Models;

public sealed record BookResponse(
    Guid Id,
    string Title,
    string Isbn,
    string Author,
    int Quantity,
    bool IsActive);