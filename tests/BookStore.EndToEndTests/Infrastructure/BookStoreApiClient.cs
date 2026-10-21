using System.Net;
using System.Net.Http.Json;
using BookStore.Api.Modules.Loans;
using BookStore.Application.Common.Models;
using BookStore.Application.Features.Books.Commands;
using BookStore.Application.Features.Users.Commands;

namespace BookStore.EndToEndTests.Infrastructure;

public sealed class BookStoreApiClient(HttpClient httpClient)
{
    public HttpClient HttpClient { get; } = httpClient;

    public async Task<CreateBookResponse> CreateBookAsync(
        CreateBookCommand command,
        Guid actorId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/books")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("X-User-Id", actorId.ToString());
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<CreateBookResponse>(cancellationToken);
        Assert.NotNull(book);
        return book;
    }

    public async Task<CreateUserResponse> CreateUserAsync(string name, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.PostAsJsonAsync(
            "/users", new CreateUserCommand(name), cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CreateUserResponse>(cancellationToken);
        Assert.NotNull(user);
        return user;
    }

    public Task<HttpResponseMessage> CreateLoanAsync(
        CreateLoanRequest request, string key, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Post, "/loans", request, actorId, correlationId, key, cancellationToken);
    }

    public Task<HttpResponseMessage> CloseLoanAsync(
        Guid loanId, string action, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Post, $"/loans/{loanId}/{action}", null,
            actorId, correlationId, null, cancellationToken);
    }

    public Task<HttpResponseMessage> UpdateQuantityAsync(
        Guid bookId, int quantity, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Patch, $"/books/{bookId}", new { Quantity = quantity },
            actorId, correlationId, null, cancellationToken);
    }

    public Task<HttpResponseMessage> DeactivateBookAsync(
        Guid bookId, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Delete, $"/books/{bookId}", null,
            actorId, correlationId, null, cancellationToken);
    }

    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        Assert.NotNull(body);
        return body;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, Guid actorId, string correlationId,
        string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        request.Headers.Add("X-User-Id", actorId.ToString());
        request.Headers.Add("X-Correlation-Id", correlationId);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await HttpClient.SendAsync(request, cancellationToken);
    }
}