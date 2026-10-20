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
}
