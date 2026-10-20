using BookStore.Application.Common.Interfaces;
using BookStore.Infrastructure.Data;
using BookStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPostgres(configuration);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IBookCopyRepository, BookCopyRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        return services;
    }

    private static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
        );
        return services;
    }
}