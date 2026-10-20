using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Interfaces;

namespace BookStore.Api.Services;

public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    private HttpContext HttpContext =>
        accessor.HttpContext
        ?? throw new InvalidOperationException(
            "Não existe uma requisição HTTP ativa.");

    public Guid ActorId
    {
        get
        {
            var values = HttpContext.Request.Headers["X-User-Id"];
            if (values.Count != 1 || !Guid.TryParse(values[0], out var actorId) || actorId == Guid.Empty)
                throw new BadRequestException(
                    "Informe um X-User-Id válido.");

            return actorId;
        }
    }

    public string CorrelationId => HttpContext.TraceIdentifier;
}