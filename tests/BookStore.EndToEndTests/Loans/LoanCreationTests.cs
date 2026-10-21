using System.Net;
using System.Net.Http.Json;
using BookStore.Api.Modules.Loans;
using BookStore.Application.Features.Books.Commands;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.EndToEndTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.EndToEndTests.Loans;

[Collection(nameof(BookStoreCollection))]
[Trait("Category", "EndToEnd")]
public sealed class LoanCreationTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Creating a loan without an idempotency key returns 400 without side effects")]
    public async Task Create_WhenIdempotencyKeyIsMissing_ShouldRejectWithoutSideEffects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorId = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(
            new CreateBookCommand("Clean Code", "9780132350884", "Robert Martin", 1),
            actorId, "prepare-book", cancellationToken);
        var user = await fixture.Primary.CreateUserAsync("Library user", cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new CreateLoanRequest(book.Id, user.Id))
        };
        request.Headers.Add("X-User-Id", actorId.ToString());
        request.Headers.Add("X-Correlation-Id", "missing-idempotency-key");

        using var response = await fixture.Secondary.HttpClient.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.NotEqual(fixture.Primary.HttpClient.BaseAddress, fixture.Secondary.HttpClient.BaseAddress);
        Assert.NotEqual(actorId, user.Id);

        await using var context = fixture.CreateDbContext();
        Assert.False(await context.Loans.AnyAsync(cancellationToken));
        Assert.False(await context.AuditEvents.AnyAsync(
            audit => audit.EntityName == nameof(Loan) && audit.Action == AuditAction.Created,
            cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}