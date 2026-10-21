using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Books.Diagnostics;

internal static class BookLogEvents
{
    internal static readonly EventId Created = new(3000, "BookCreated");
    internal static readonly EventId Updated = new(3001, "BookUpdated");
    internal static readonly EventId Deactivated = new(3002, "BookDeactivated");
    internal static readonly EventId QuantityRejected = new(3003, "BookQuantityRejected");
    internal static readonly EventId Unchanged = new(3004, "BookUnchanged");
    internal static readonly EventId CreationRejected = new(3005, "BookCreationRejected");
    internal static readonly EventId CatalogCacheRead = new(3010, "CatalogCacheRead");
}
