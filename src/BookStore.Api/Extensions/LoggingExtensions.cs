using System.Diagnostics;
using Serilog;

namespace BookStore.Api.Extensions;

public static class LoggingExtensions
{
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSerilog((serviceProvider, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(serviceProvider)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "BookStore.Api")
            .Enrich.WithProperty(
                "Environment",
                serviceProvider
                    .GetRequiredService<IHostEnvironment>()
                    .EnvironmentName));

        return services;
    }

    public static WebApplication UseStructuredRequestLogging(
        this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, context) =>
            {
                diagnosticContext.Set(
                    "correlationId",
                    context.TraceIdentifier);

                var activity = Activity.Current;
                if (activity is null)
                    return;

                diagnosticContext.Set(
                    "TraceId",
                    activity.TraceId.ToString());

                diagnosticContext.Set(
                    "SpanId",
                    activity.SpanId.ToString());
            };
        });

        return app;
    }
}