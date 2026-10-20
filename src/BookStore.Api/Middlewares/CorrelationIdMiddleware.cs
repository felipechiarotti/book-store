using BookStore.Application.Common.Exceptions;

namespace BookStore.Api.Middlewares;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var value = values.Count == 1 ? values[0] : null;

            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 128 ||
                value.Any(char.IsControl))
            {
                throw new BadRequestException(
                    "Informe um X-Correlation-Id válido, com até 128 caracteres.");
            }

            context.TraceIdentifier = value;
        }

        using var scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["correlationId"] = context.TraceIdentifier
            });

        await next(context);
    }
}