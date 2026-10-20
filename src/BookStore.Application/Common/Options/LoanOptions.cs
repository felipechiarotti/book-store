namespace BookStore.Application.Common.Options;

public sealed class LoanOptions
{
    public const string SectionName = "Loans";

    public int DurationDays { get; init; } = 7;
}