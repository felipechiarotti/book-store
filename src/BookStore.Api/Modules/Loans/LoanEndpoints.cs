using BookStore.Api.Filters;
using BookStore.Application.Common.Exceptions;
using BookStore.Application.Common.Models;
using BookStore.Application.Features.Loans.Commands;
using BookStore.Application.Features.Loans.Queries;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Modules.Loans;

public sealed record CreateLoanRequest(Guid BookId, Guid UserId);

public static class LoanEndpoints
{
    public static void MapLoanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/loans")
            .WithTags("Loans");

        group.MapPost("", CreateAsync)
            .AddEndpointFilter<RequireActorFilter>()
            .WithName("CreateLoan")
            .WithSummary("Registra o empréstimo de uma unidade disponível.")
            .WithDescription("Exige Idempotency-Key e X-User-Id com o GUID do ator declarado.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetLoanById")
            .WithSummary("Consulta um empréstimo pelo identificador.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/{id:guid}/return", ReturnAsync)
            .AddEndpointFilter<RequireActorFilter>()
            .WithName("ReturnLoan")
            .WithSummary("Registra a devolução de um empréstimo.")
            .WithDescription("Exige X-User-Id. Repetir uma devolução confirmada não gera novos efeitos.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/{id:guid}/cancel", CancelAsync)
            .AddEndpointFilter<RequireActorFilter>()
            .WithName("CancelLoan")
            .WithSummary("Cancela um empréstimo preservando seu histórico.")
            .WithDescription("Exige X-User-Id. Repetir um cancelamento confirmado não gera novos efeitos.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Created<CreateLoanResponse>> CreateAsync(
        CreateLoanRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpRequest httpRequest,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (httpRequest.Headers["Idempotency-Key"].Count != 1)
            throw new BadRequestException("Informe uma única chave no cabeçalho Idempotency-Key.");

        var response = await sender.Send(
            new CreateLoanCommand(request.BookId, request.UserId, idempotencyKey ?? string.Empty),
            cancellationToken);

        return TypedResults.Created($"/loans/{response.LoanId}", response);
    }

    private static async Task<Ok<LoanResponse>> GetByIdAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetLoanByIdQuery(id), cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<NoContent> ReturnAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ReturnLoanCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> CancelAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new CancelLoanCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }
}