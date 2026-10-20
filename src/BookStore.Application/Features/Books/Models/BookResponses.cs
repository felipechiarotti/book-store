using BookStore.Domain.Enums;

namespace BookStore.Application.Features.Books.Models;

public sealed record BookCreatedResponse(Guid Id);

public sealed record BookResponse(
    Guid Id,
    string Title,
    string Isbn,
    string Author,
    bool IsActive,
    int Quantity);
