using BookStore.Api.Filters;
using BookStore.Application.Features.Books.Commands;
using BookStore.Application.Features.Books.Models;
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
    }

    private static async Task<Created<BookCreatedResponse>> CreateAsync(
        CreateBookCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var bookCreatedResponse = await sender.Send(command, cancellationToken);

        return TypedResults.Created(
            $"/books/{bookCreatedResponse.Id}",
            bookCreatedResponse);
    }
}