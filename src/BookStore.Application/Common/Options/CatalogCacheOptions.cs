namespace BookStore.Application.Common.Options;

public sealed class CatalogCacheOptions
{
    public const string SectionName = "CatalogCache";

    public int TimeToLiveSeconds { get; init; }
    public int OperationTimeoutMilliseconds { get; init; }
}