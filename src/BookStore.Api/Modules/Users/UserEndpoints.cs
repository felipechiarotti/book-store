using BookStore.Application.Features.Users.Commands;
using BookStore.Application.Common.Models;
using BookStore.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Modules.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users");

        group.MapGet("/{id:guid}/loans", GetLoansAsync)
            .WithName("GetUserLoans")
            .WithSummary("Consulta o histórico paginado de empréstimos de um usuário.")
            .WithDescription(
                "Inclui empréstimos ativos, devolvidos e cancelados, inclusive de livros desativados. " +
                "Ordenação dos mais recentes aos mais antigos; página padrão 1, tamanho padrão configurado pela aplicação e máximo 100.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("", CreateAsync)
            .WithName("CreateUser")
            .WithSummary("Cadastra um usuário.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Ok<PageResponse<LoanHistoryResponse>>> GetLoansAsync(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null)
    {
        var response = await sender.Send(
            new GetUserLoansQuery(id, page, pageSize),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<Created<CreateUserResponse>> CreateAsync(
        CreateUserCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(command, cancellationToken);

        return TypedResults.Created((string?)null, response);
    }
}
