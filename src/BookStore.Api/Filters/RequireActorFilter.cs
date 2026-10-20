using BookStore.Application.Common.Interfaces;

namespace BookStore.Api.Filters;

public sealed class RequireActorFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var requestContext = context.HttpContext.RequestServices
            .GetRequiredService<IRequestContext>();

        _ = requestContext.ActorId;

        return next(context);
    }
}