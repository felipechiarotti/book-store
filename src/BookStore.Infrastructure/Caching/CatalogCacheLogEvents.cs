using Microsoft.Extensions.Logging;

namespace BookStore.Infrastructure.Caching;

internal static class CatalogCacheLogEvents
{
    internal static readonly EventId Degraded = new(5000, "CatalogCacheDegraded");
    internal static readonly EventId PayloadRejected = new(5001, "CatalogCachePayloadRejected");
    internal static readonly EventId OperationCompleted = new(5002, "CatalogCacheOperationCompleted");
}
