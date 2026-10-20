using BookStore.Application.Common.Models;
using BookStore.Application.Features.AuditEvents.Queries;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Modules.AuditEvents;

public static class AuditEventEndpoints
{
    public static void MapAuditEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/audit-events")
            .WithTags("Audit events")
            .MapGet("", GetAsync)
            .WithName("GetAuditEvents")
            .WithSummary("Consulta a trilha de auditoria com paginação.")
            .WithDescription(
                "Filtros opcionais e exatos por entityName, entityId e correlationId, combinados por AND. " +
                "Ordenação por data e identificador decrescentes; página padrão 1, tamanho padrão configurado pela aplicação e máximo 100. " +
                "Details contém o JSON armazenado como string.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Ok<PageResponse<AuditEventResponse>>> GetAsync(
        [AsParameters] GetAuditEventsQuery query,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(query, cancellationToken);
        return TypedResults.Ok(response);
    }
}
