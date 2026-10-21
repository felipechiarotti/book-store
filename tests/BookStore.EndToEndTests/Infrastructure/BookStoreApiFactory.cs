using BookStore.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BookStore.EndToEndTests.Infrastructure;

internal sealed class BookStoreApiFactory(
    string postgresConnectionString,
    string redisConnectionString) : WebApplicationFactory<Program>
{
    internal string PostgresConnectionString { get; } = postgresConnectionString;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = PostgresConnectionString,
                ["ConnectionStrings:Redis"] = redisConnectionString,
                ["Loans:DurationDays"] = "7",
                ["Pagination:DefaultPageSize"] = "20",
                ["CatalogCache:TimeToLiveSeconds"] = "60",
                ["CatalogCache:OperationTimeoutMilliseconds"] = "200",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty
            }));

        return base.CreateHost(builder);
    }
}