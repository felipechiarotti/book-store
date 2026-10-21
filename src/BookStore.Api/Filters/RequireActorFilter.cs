using BookStore.Application.Common.Interfaces;

namespace BookStore.Api.Filters;

public sealed class RequireActorFilter(ILogger<RequireActorFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var requestContext = context.HttpContext.RequestServices
            .GetRequiredService<IRequestContext>();

        var actorId = requestContext.ActorId;
        context.HttpContext.Items[nameof(IRequestContext.ActorId)] = actorId;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            [nameof(IRequestContext.ActorId)] = actorId
        });
        return await next(context);
    }
}
