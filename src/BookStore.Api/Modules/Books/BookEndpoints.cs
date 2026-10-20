using BookStore.Api.Filters;
using BookStore.Application.Common.Models;
using BookStore.Application.Features.Books.Commands;

using BookStore.Application.Features.Books.Queries;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Modules.Books;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/books")
            .WithTags("Books");

        group.MapPost("", CreateAsync)
            .AddEndpointFilter<RequireActorFilter>()
            .WithName("CreateBook")
            .WithSummary("Cadastra um livro e seus exemplares.")
            .WithDescription(
                "Exige X-User-Id com um GUID não vazio para identificar o ator.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id:guid}", UpdateAsync)
            .AddEndpointFilter<RequireActorFilter>()
            .WithName("UpdateBook")
            .WithSummary("Atualiza parcialmente título, autor e quantidade do livro.")
            .WithDescription(
                "Exige X-User-Id. Campos ausentes são preservados; null explícito é rejeitado. " +
                "A quantidade não pode ficar abaixo dos empréstimos ativos. ISBN não é editável.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapDelete("/{id:guid}", DeactivateAsync)
            .AddEndpointFilter<RequireActorFilter>()
            .WithName("DeactivateBook")
            .WithSummary("Desativa um livro, preservando os empréstimos e o histórico.")
            .WithDescription(
                "Exige X-User-Id. Impede novos empréstimos, sem encerrar os existentes. " +
                "Repetir a desativação retorna 204 sem novo evento de auditoria.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetBookById")
            .WithSummary("Consulta um livro pelo identificador.")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("", GetAsync)
            .WithName("GetBooks")
            .WithSummary("Lista os livros com paginação.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id:guid}/availability", GetAvailabilityAsync)
            .WithName("GetBookAvailability")
            .WithSummary("Consulta a disponibilidade atual de um livro.")
            .WithDescription("A consulta informa a disponibilidade, sem reservar exemplares.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Created<CreateBookResponse>> CreateAsync(
        CreateBookCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var bookCreatedResponse = await sender.Send(command, cancellationToken);

        return TypedResults.Created(
            $"/books/{bookCreatedResponse.Id}",
            bookCreatedResponse);
    }

    private static async Task<NoContent> UpdateAsync(
        Guid id,
        UpdateBookRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateBookCommand(id, request.Title, request.Author, request.Quantity),
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeactivateAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateBookCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BookResponse>> GetByIdAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetBookByIdQuery(id),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<PageResponse<BookResponse>>> GetAsync(
        [AsParameters] GetBooksQuery query,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(query, cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<BookAvailabilityResponse>> GetAvailabilityAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetBookAvailabilityQuery(id),
            cancellationToken);

        return TypedResults.Ok(response);
    }
}
