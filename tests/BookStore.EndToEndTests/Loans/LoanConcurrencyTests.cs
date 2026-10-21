using System.Net;
using System.Net.Http.Json;
using BookStore.Api.Modules.Loans;
using BookStore.Application.Common.Models;
using BookStore.Application.Features.Loans.Commands;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.EndToEndTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookStore.EndToEndTests.Loans;

[Collection(nameof(BookStoreCollection))]
[Trait("Category", "EndToEnd")]
public sealed class LoanConcurrencyTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Create_WhenTwoInstancesCompete_ShouldRespectCapacity(int quantity)
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(
            new TestBookBuilder().WithQuantity(quantity).Build(), actor, "book", token);
        var firstUser = await fixture.Primary.CreateUserAsync("First user", token);
        var secondUser = await fixture.Secondary.CreateUserAsync("Second user", token);
        await using var gate = await fixture.LockBookAsync(book.Id, token);

        using var responses = await gate.ExecuteAsync(
            () => fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, firstUser.Id), "first", actor, "first",
                token),
            () => fixture.Secondary.CreateLoanAsync(new CreateLoanRequest(book.Id, secondUser.Id), "second", actor,
                "second", token),
            token);

        var statuses = new[] { responses.First.StatusCode, responses.Second.StatusCode };
        Assert.Equal(quantity, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(2 - quantity, statuses.Count(status => status == HttpStatusCode.Conflict));
        var availability = await fixture.Primary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(quantity, availability.Quantity);
        Assert.Equal(quantity, availability.ActiveLoans);
        Assert.Equal(0, availability.AvailableQuantity);

        await using var context = fixture.CreateDbContext();
        var loans = await context.Loans.AsNoTracking().ToListAsync(token);
        Assert.Equal(quantity, loans.Count);
        Assert.All(loans, loan =>
        {
            Assert.Equal(book.Id, loan.BookId);
            Assert.Equal(LoanStatus.Active, loan.Status);
            Assert.Equal(TimeSpan.FromDays(7), loan.DueAt - loan.LoanedAt);
        });
        Assert.Equal(quantity, await context.AuditEvents.CountAsync(
            audit => audit.EntityName == nameof(Loan) && audit.Action == AuditAction.Created, token));
        foreach (var loan in loans)
            await AuditAssertions.SingleAsync(context, nameof(Loan), loan.Id, AuditAction.Created,
                actor, loan.IdempotencyKey, token);
    }

    [Fact]
    public async Task Create_WhenQuantityIsReducedConcurrently_ShouldProduceASerializableOutcome()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(
            new TestBookBuilder().WithQuantity(2).Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        using var initial = await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "initial",
            actor, "initial", token);
        Assert.Equal(HttpStatusCode.Created, initial.StatusCode);
        await using var gate = await fixture.LockBookAsync(book.Id, token);

        using var responses = await gate.ExecuteAsync(
            () => fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "next", actor, "next",
                token),
            () => fixture.Secondary.UpdateQuantityAsync(book.Id, 1, actor, "reduce", token), token);

        await using var context = fixture.CreateDbContext();
        var storedBook = await context.Books.AsNoTracking().SingleAsync(token);
        var activeCount = await context.Loans.CountAsync(loan => loan.Status == LoanStatus.Active, token);
        if (responses.First.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal(HttpStatusCode.Conflict, responses.Second.StatusCode);
            Assert.Equal(2, storedBook.Quantity);
            Assert.Equal(2, activeCount);
            Assert.False(await context.AuditEvents.AnyAsync(audit => audit.Action == AuditAction.Updated, token));
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, responses.First.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, responses.Second.StatusCode);
            Assert.Equal(1, storedBook.Quantity);
            Assert.Equal(1, activeCount);
            await AuditAssertions.SingleAsync(context, nameof(Book), book.Id, AuditAction.Updated, actor, "reduce",
                token);
        }

        Assert.True(activeCount <= storedBook.Quantity);
        Assert.Equal(activeCount, await context.AuditEvents.CountAsync(
            audit => audit.EntityName == nameof(Loan) && audit.Action == AuditAction.Created, token));
        var availability = await fixture.Secondary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(0, availability.AvailableQuantity);
    }

    [Theory]
    [InlineData("return", "return", LoanStatus.Returned)]
    [InlineData("cancel", "cancel", LoanStatus.Cancelled)]
    [InlineData("return", "cancel", null)]
    public async Task Close_WhenTwoInstancesCompete_ShouldPersistOnlyOneTransition(
        string firstAction, string secondAction, LoanStatus? expectedStatus)
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        using var created =
            await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "loan", actor, "loan",
                token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var loan = await created.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        Assert.NotNull(loan);
        await using var gate = await fixture.LockBookAsync(book.Id, token);

        using var responses = await gate.ExecuteAsync(
            () => fixture.Primary.CloseLoanAsync(loan.LoanId, firstAction, actor, "first-close", token),
            () => fixture.Secondary.CloseLoanAsync(loan.LoanId, secondAction, actor, "second-close", token), token);

        await using var context = fixture.CreateDbContext();
        var stored = await context.Loans.AsNoTracking().SingleAsync(token);
        if (firstAction == secondAction)
        {
            Assert.Equal(HttpStatusCode.NoContent, responses.First.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, responses.Second.StatusCode);
            Assert.Equal(expectedStatus, stored.Status);
        }
        else
        {
            Assert.Contains(responses.First.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });
            Assert.Equal(responses.First.StatusCode == HttpStatusCode.NoContent
                ? HttpStatusCode.Conflict
                : HttpStatusCode.NoContent, responses.Second.StatusCode);
            Assert.Equal(responses.First.StatusCode == HttpStatusCode.NoContent
                ? LoanStatus.Returned
                : LoanStatus.Cancelled, stored.Status);
        }

        Assert.NotNull(stored.ClosedAt);
        var events = await context.AuditEvents.AsNoTracking().Where(audit =>
            audit.EntityId == loan.LoanId && audit.Action != AuditAction.Created).ToListAsync(token);
        var closed = Assert.Single(events);
        Assert.Equal(stored.Status == LoanStatus.Returned ? AuditAction.Returned : AuditAction.Cancelled,
            closed.Action);
        await AuditAssertions.SingleAsync(context, nameof(Loan), stored.Id, closed.Action, actor, closed.CorrelationId,
            token);
        Assert.Contains(closed.CorrelationId, new[] { "first-close", "second-close" });
        if (firstAction != secondAction)
            Assert.Equal(responses.First.StatusCode == HttpStatusCode.NoContent ? "first-close" : "second-close",
                closed.CorrelationId);
        var availability = await fixture.Primary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(1, availability.Quantity);
        Assert.Equal(0, availability.ActiveLoans);
        Assert.Equal(1, availability.AvailableQuantity);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}