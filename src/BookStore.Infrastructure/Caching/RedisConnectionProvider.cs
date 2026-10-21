using BookStore.Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BookStore.Infrastructure.Caching;

internal sealed class RedisConnectionProvider : IAsyncDisposable
{
    private readonly Lock _sync = new();
    private readonly ConfigurationOptions _configuration;
    private Task<ConnectionMultiplexer>? _connectionTask;
    private bool _disposed;

    public RedisConnectionProvider(IConfiguration configuration, IOptions<CatalogCacheOptions> options)
    {
        var connectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:Redis.");

        try
        {
            _configuration = ConfigurationOptions.Parse(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("A configuração ConnectionStrings:Redis é inválida.");
        }

        _configuration.AbortOnConnectFail = false;
        _configuration.BacklogPolicy = BacklogPolicy.FailFast;
        _configuration.ConnectRetry = 0;
        _configuration.ConnectTimeout = options.Value.OperationTimeoutMilliseconds;
        _configuration.AsyncTimeout = options.Value.OperationTimeoutMilliseconds;
    }

    public async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<ConnectionMultiplexer> connectionTask;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_connectionTask is null || _connectionTask.IsFaulted || _connectionTask.IsCanceled)
            {
                _ = _connectionTask?.Exception;
                _connectionTask = ConnectionMultiplexer.ConnectAsync(_configuration);
            }

            connectionTask = _connectionTask;
        }

        var connection = await connectionTask.WaitAsync(cancellationToken);
        return connection.GetDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        Task<ConnectionMultiplexer>? connectionTask;
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            connectionTask = _connectionTask;
        }

        if (connectionTask is null)
            return;

        try
        {
            var connection = await connectionTask;
            connection.Dispose();
        }
        catch (RedisException)
        {
        }
    }
}
