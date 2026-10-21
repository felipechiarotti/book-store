using System.Globalization;
using System.Text.Json;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Models;
using BookStore.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BookStore.Infrastructure.Caching;

internal sealed class RedisBookCatalogCache(
    RedisConnectionProvider connectionProvider,
    IOptions<CatalogCacheOptions> options,
    ILogger<RedisBookCatalogCache> logger) : IBookCatalogCache
{
    private const string Prefix = "bookstore:catalog:v1";
    private const string GenerationKey = Prefix + ":generation";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public Task<string?> GetGenerationAsync(CancellationToken cancellationToken)
        => ExecuteAsync<string?>("GetGeneration", async (database, token) =>
        {
            var current = await database.StringGetAsync(GenerationKey).WaitAsync(token);
            if (!current.IsNull)
                return ReadGeneration(current);

            var candidate = Guid.NewGuid().ToString("N");
            var created = await database.StringSetAsync(
                GenerationKey, candidate, when: When.NotExists).WaitAsync(token);

            if (created)
                return candidate;

            return ReadGeneration(await database.StringGetAsync(GenerationKey).WaitAsync(token));
        }, null, cancellationToken);

    public Task<PageResponse<BookResponse>?> GetPageAsync(
        string generation, int page, int pageSize, CancellationToken cancellationToken)
        => ExecuteAsync<PageResponse<BookResponse>?>("GetPage", async (database, token) =>
        {
            var value = await database.StringGetAsync(PageKey(generation, page, pageSize)).WaitAsync(token);
            if (value.IsNull)
                return null;

            try
            {
                var result = JsonSerializer.Deserialize<PageResponse<BookResponse>>(
                    value.ToString(), SerializerOptions);

                if (result?.Items is not null && result.Page == page && result.PageSize == pageSize &&
                    result.TotalCount >= 0 && result.Items.Count <= pageSize &&
                    result.Items.All(book => book is not null && book.Id != Guid.Empty &&
                        book.Quantity >= 0 && book.Title is not null &&
                        book.Isbn is not null && book.Author is not null))
                    return result;
            }
            catch (JsonException)
            {
            }

            logger.LogWarning("Ignoring an incompatible Redis catalog page.");
            return null;
        }, null, cancellationToken);

    public async Task SetPageAsync(
        string generation, PageResponse<BookResponse> page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(page, SerializerOptions);
        await ExecuteAsync("SetPage", async (database, token) =>
            await database.StringSetAsync(
                PageKey(generation, page.Page, page.PageSize), payload,
                TimeSpan.FromSeconds(options.Value.TimeToLiveSeconds)).WaitAsync(token),
            false, cancellationToken);
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        var generation = Guid.NewGuid().ToString("N");
        await ExecuteAsync("Invalidate", async (database, token) =>
            await database.StringSetAsync(GenerationKey, generation).WaitAsync(token),
            false, cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(
        string operation,
        Func<IDatabase, CancellationToken, Task<T>> action,
        T fallback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(options.Value.OperationTimeoutMilliseconds));

        try
        {
            var database = await connectionProvider.GetDatabaseAsync(timeout.Token);
            var result = await action(database, timeout.Token);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Redis catalog operation {Operation} exceeded its time budget.", operation);
        }
        catch (RedisException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("Redis catalog operation {Operation} failed with {FailureType}.",
                operation, exception.GetType().Name);
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("Redis catalog operation {Operation} timed out.", operation);
        }

        return fallback;
    }

    private static string? ReadGeneration(RedisValue value)
    {
        var generation = (string?)value;
        return Guid.TryParseExact(generation, "N", out var id) && id != Guid.Empty ? generation : null;
    }

    private static string PageKey(string generation, int page, int pageSize)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}:{generation}:page:{page}:size:{pageSize}");
}
