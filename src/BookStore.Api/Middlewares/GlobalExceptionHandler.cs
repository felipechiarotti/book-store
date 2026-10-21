using BookStore.Application.Common.Exceptions;
using FluentValidation;
using Npgsql;
using BookStore.Application.Common.Interfaces;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Middlewares;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException ve => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Erro de validação",
                Detail = "Um ou mais campos são inválidos.",
                Extensions =
                {
                    ["errors"] = ve.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray())
                }
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Não encontrado",
                Detail = exception.Message
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito",
                Detail = exception.Message
            },
            BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitação inválida",
                Detail = "O corpo da requisição é inválido. Confira o JSON e os tipos dos campos."
            },
            BadRequestException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitação inválida",
                Detail = exception.Message
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = "Ocorreu um erro inesperado. Tente novamente."
            }
        };

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        problemDetails.Extensions["correlationId"] =
            httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
