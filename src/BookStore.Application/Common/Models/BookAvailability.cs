namespace BookStore.Application.Common.Models;

public sealed record BookAvailability(
    int Quantity,
    int ActiveLoans,
    bool IsActive);
