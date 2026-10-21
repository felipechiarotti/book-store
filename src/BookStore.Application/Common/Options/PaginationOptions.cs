namespace BookStore.Application.Common.Options;

public sealed class PaginationOptions
{
    public const string SectionName = "Pagination";

    public int DefaultPageSize { get; init; } = 20;
}