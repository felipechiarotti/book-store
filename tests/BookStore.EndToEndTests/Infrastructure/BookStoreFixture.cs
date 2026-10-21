using BookStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace BookStore.EndToEndTests.Infrastructure;

public sealed class BookStoreFixture : IAsyncLifetime
{
    internal const string PrimaryApplicationName = "BookStoreTests.Primary";
    internal const string SecondaryApplicationName = "BookStoreTests.Secondary";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("bookstore_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine").Build();
    private BookStoreApiFactory? _primaryFactory;
    private BookStoreApiFactory? _secondaryFactory;
    private BookStoreApiClient? _primary;
    private BookStoreApiClient? _secondary;
    private bool _initialized;
    private bool _disposed;

    public BookStoreApiClient Primary => _primary
                                         ?? throw new InvalidOperationException(
                                             "A API primária ainda não foi iniciada.");

    public BookStoreApiClient Secondary => _secondary
                                           ?? throw new InvalidOperationException(
                                               "A API secundária ainda não foi iniciada.");

    public async ValueTask InitializeAsync()
    {
        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cancellationToken = startupTimeout.Token;

        try
        {
            await _postgres.StartAsync(cancellationToken);
            await _redis.StartAsync(cancellationToken);

            await using (var context = CreateDbContext())
            {
                await context.Database.MigrateAsync(cancellationToken);
            }

            _primaryFactory = CreateFactory(PrimaryApplicationName);
            _primary = CreateClient(_primaryFactory);
            _secondaryFactory = CreateFactory(SecondaryApplicationName);
            _secondary = CreateClient(_secondaryFactory);
            _initialized = true;
        }
        catch (Exception initializationError)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Falha ao iniciar e descartar a infraestrutura de testes.",
                    initializationError, cleanupError);
            }

            throw;
        }
    }

    public AppDbContext CreateDbContext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new AppDbContext(options, TimeProvider.System);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("O reset exige os containers iniciados por esta fixture.");

        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE audit_events, loans, users, books;", cancellationToken);

        var result = await _redis.ExecAsync(["redis-cli", "FLUSHDB"], cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Não foi possível limpar o Redis descartável dos testes.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _initialized = false;

        try
        {
            await Task.WhenAll(
                _secondaryFactory?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                _primaryFactory?.DisposeAsync().AsTask() ?? Task.CompletedTask);
        }
        finally
        {
            await Task.WhenAll(_redis.DisposeAsync().AsTask(), _postgres.DisposeAsync().AsTask());
        }
    }

    internal Task<DatabaseLockGate> LockBookAsync(Guid bookId, CancellationToken cancellationToken)
    {
        return DatabaseLockGate.HoldBookAsync(_postgres.GetConnectionString(), bookId, cancellationToken);
    }

    internal Task<DatabaseLockGate> BlockLoanInsertsAsync(CancellationToken cancellationToken)
    {
        return DatabaseLockGate.HoldLoanInsertsAsync(_postgres.GetConnectionString(), cancellationToken);
    }

    internal Task<AuditInsertFailure> FailLoanAuditAsync(CancellationToken cancellationToken)
    {
        return AuditInsertFailure.InstallAsync(_postgres.GetConnectionString(), cancellationToken);
    }

    internal async Task<string?> GetCatalogGenerationAsync(CancellationToken cancellationToken)
    {
        var value = await ExecuteRedisAsync(["GET", "bookstore:catalog:v1:generation"], cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    internal async Task<bool> CatalogPageExistsAsync(
        string generation, int page, int pageSize, CancellationToken cancellationToken)
    {
        return await ExecuteRedisAsync(
            ["EXISTS", $"bookstore:catalog:v1:{generation}:page:{page}:size:{pageSize}"], cancellationToken) == "1";
    }

    private async Task<string> ExecuteRedisAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var result = await _redis.ExecAsync(["redis-cli", "--raw", .. arguments], cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Falha ao inspecionar o Redis descartável dos testes.");
        return result.Stdout.Trim();
    }

    private BookStoreApiFactory CreateFactory(string applicationName)
    {
        var connection = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            ApplicationName = applicationName
        };
        var factory = new BookStoreApiFactory(connection.ConnectionString, _redis.GetConnectionString());
        factory.UseKestrel(0);
        return factory;
    }

    private BookStoreApiClient CreateClient(BookStoreApiFactory factory)
    {
        factory.ClientOptions.AllowAutoRedirect = false;
        factory.ClientOptions.HandleCookies = false;
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (context.Database.GetConnectionString() != factory.PostgresConnectionString ||
            configuration.GetConnectionString("Redis") != _redis.GetConnectionString())
            throw new InvalidOperationException("A API deve utilizar somente os containers desta fixture.");

        return new BookStoreApiClient(client);
    }
}