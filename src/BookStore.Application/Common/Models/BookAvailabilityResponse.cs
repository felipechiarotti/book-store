namespace BookStore.Application.Common.Models;

public sealed record BookAvailabilityResponse(
    Guid BookId,
    int Quantity,
    int ActiveLoans,
    int AvailableQuantity,
    bool IsActive);
