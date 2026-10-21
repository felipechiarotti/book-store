using BookStore.Api.Diagnostics;
using BookStore.Application.Features.Loans.Diagnostics;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BookStore.Api.Extensions;

public static class TelemetryExtensions
{
    public static IServiceCollection AddApplicationTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var exportEnabled = !string.IsNullOrWhiteSpace(endpoint);
        var protocol = OtlpExportProtocol.HttpProtobuf;

        if (exportEnabled)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
                endpointUri.Scheme is not ("http" or "https") ||
                string.IsNullOrWhiteSpace(endpointUri.Host) ||
                !string.IsNullOrEmpty(endpointUri.UserInfo) ||
                !string.IsNullOrEmpty(endpointUri.Query) ||
                !string.IsNullOrEmpty(endpointUri.Fragment))
                throw new InvalidOperationException(
                    "Configure um endpoint OTLP HTTP/HTTPS válido, sem credenciais, query ou fragmento.");

            protocol = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]?.Trim() switch
            {
                null or "" or "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                "grpc" => OtlpExportProtocol.Grpc,
                _ => throw new InvalidOperationException("O protocolo OTLP deve ser grpc ou http/protobuf.")
            };
        }

        var serviceName = configuration["OTEL_SERVICE_NAME"];
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = "BookStore.Api";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.RecordException = false)
                    .AddNpgsql()
                    .AddProcessor(_ => new TracePrivacyProcessor());

                if (exportEnabled) tracing.AddOtlpExporter(options => options.Protocol = protocol);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(LoanMetrics.MeterName)
                    .AddAspNetCoreInstrumentation();

                if (exportEnabled)
                    metrics.AddOtlpExporter(options => options.Protocol = protocol);
            });

        return services;
    }
}