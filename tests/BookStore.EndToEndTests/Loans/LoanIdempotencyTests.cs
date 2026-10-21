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
public sealed class LoanIdempotencyTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Create_WhenSameRequestIsConcurrent_ShouldReplayOneLoan()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        var request = new CreateLoanRequest(book.Id, user.Id);
        await using var gate = await fixture.LockBookAsync(book.Id, token);

        using var responses = await gate.ExecuteAsync(
            () => fixture.Primary.CreateLoanAsync(request, "shared", actor, "first", token),
            () => fixture.Secondary.CreateLoanAsync(request, "shared", actor, "second", token), token);

        Assert.Equal(HttpStatusCode.Created, responses.First.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responses.Second.StatusCode);
        var first = await responses.First.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        var second = await responses.Second.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        Assert.NotNull(first);
        AssertEquivalentCreation(first, second);
        Assert.Equal(responses.First.Headers.Location, responses.Second.Headers.Location);
        await using var context = fixture.CreateDbContext();
        var loan = Assert.Single(await context.Loans.AsNoTracking().ToListAsync(token));
        Assert.Equal(first.LoanId, loan.Id);
        Assert.Equal("shared", loan.IdempotencyKey);
        var audit = Assert.Single(await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.EntityName == nameof(Loan)).ToListAsync(token));
        Assert.Contains(audit.CorrelationId, new[] { "first", "second" });
        await AuditAssertions.SingleAsync(context, nameof(Loan), loan.Id, AuditAction.Created,
            actor, audit.CorrelationId, token);
        var availability = await fixture.Primary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(1, availability.Quantity);
        Assert.Equal(1, availability.ActiveLoans);
        Assert.Equal(0, availability.AvailableQuantity);
    }

    [Fact]
    public async Task Create_WhenReplayedAfterReturn_ShouldPreserveOriginalResponseAndAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        var request = new CreateLoanRequest(book.Id, user.Id);
        using var created = await fixture.Primary.CreateLoanAsync(request, "original", actor, "original", token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        Assert.NotNull(original);
        using var returned = await fixture.Primary.CloseLoanAsync(original.LoanId, "return", actor, "return", token);
        Assert.Equal(HttpStatusCode.NoContent, returned.StatusCode);

        using var replay = await fixture.Secondary.CreateLoanAsync(
            request, "original", Guid.NewGuid(), "replay", token);

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        AssertEquivalentCreation(original, await replay.Content.ReadFromJsonAsync<CreateLoanResponse>(token));
        Assert.Equal(created.Headers.Location, replay.Headers.Location);
        Assert.Equal(TimeSpan.FromDays(7), original.DueAt - original.LoanedAt);
        await using var context = fixture.CreateDbContext();
        var loan = Assert.Single(await context.Loans.AsNoTracking().ToListAsync(token));
        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.NotNull(loan.ClosedAt);
        await AuditAssertions.SingleAsync(context, nameof(Loan), loan.Id, AuditAction.Created, actor, "original",
            token);
        Assert.Equal(2, await context.AuditEvents.CountAsync(audit => audit.EntityId == loan.Id, token));
        var availability = await fixture.Secondary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(1, availability.AvailableQuantity);
    }

    [Theory]
    [InlineData(nameof(CreateLoanRequest.BookId))]
    [InlineData(nameof(CreateLoanRequest.UserId))]
    public async Task Create_WhenKeyIsReusedWithDifferentData_ShouldConflict(string changedProperty)
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(
            new TestBookBuilder().WithQuantity(2).Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Original user", token);
        using var original = await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "shared",
            actor, "original", token);
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        var differentRequest = new CreateLoanRequest(book.Id, user.Id);
        if (changedProperty == nameof(CreateLoanRequest.BookId))
        {
            var otherBook = await fixture.Primary.CreateBookAsync(
                new TestBookBuilder().WithIsbn("9780201633610").Build(), actor, "other-book", token);
            differentRequest = differentRequest with { BookId = otherBook.Id };
        }
        else
        {
            var otherUser = await fixture.Primary.CreateUserAsync("Other user", token);
            differentRequest = differentRequest with { UserId = otherUser.Id };
        }

        using var conflict =
            await fixture.Secondary.CreateLoanAsync(differentRequest, "shared", actor, "conflict", token);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await using var context = fixture.CreateDbContext();
        Assert.Single(await context.Loans.AsNoTracking().ToListAsync(token));
        Assert.Equal(1, await context.AuditEvents.CountAsync(audit => audit.EntityName == nameof(Loan), token));
        var availability = await fixture.Primary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(1, availability.ActiveLoans);
        Assert.Equal(1, availability.AvailableQuantity);
    }

    [Fact]
    public async Task Create_WhenDifferentBooksRaceForTheSameKey_ShouldPersistOnlyTheWinner()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var firstBook = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book-one", token);
        var secondBook = await fixture.Secondary.CreateBookAsync(
            new TestBookBuilder().WithIsbn("9780201633610").Build(), actor, "book-two", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        await using var gate = await fixture.BlockLoanInsertsAsync(token);

        using var responses = await gate.ExecuteAsync(
            () => fixture.Primary.CreateLoanAsync(new CreateLoanRequest(firstBook.Id, user.Id), "global", actor,
                "first", token),
            () => fixture.Secondary.CreateLoanAsync(new CreateLoanRequest(secondBook.Id, user.Id), "global", actor,
                "second", token), token);

        Assert.Contains(responses.First.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        Assert.Equal(
            responses.First.StatusCode == HttpStatusCode.Created ? HttpStatusCode.Conflict : HttpStatusCode.Created,
            responses.Second.StatusCode);
        await using var context = fixture.CreateDbContext();
        var winner = Assert.Single(await context.Loans.AsNoTracking().ToListAsync(token));
        var firstWon = responses.First.StatusCode == HttpStatusCode.Created;
        Assert.Equal(firstWon ? firstBook.Id : secondBook.Id, winner.BookId);
        Assert.Equal("global", winner.IdempotencyKey);
        Assert.Equal(1, await context.AuditEvents.CountAsync(audit => audit.EntityName == nameof(Loan), token));
        await AuditAssertions.SingleAsync(context, nameof(Loan), winner.Id, AuditAction.Created,
            actor, firstWon ? "first" : "second", token);
        var winnerAvailability = await fixture.Primary.GetAsync<BookAvailabilityResponse>(
            $"/books/{winner.BookId}/availability", token);
        var loserAvailability = await fixture.Secondary.GetAsync<BookAvailabilityResponse>(
            $"/books/{(firstWon ? secondBook.Id : firstBook.Id)}/availability", token);
        Assert.Equal(0, winnerAvailability.AvailableQuantity);
        Assert.Equal(1, loserAvailability.AvailableQuantity);
        Assert.Equal(0, loserAvailability.ActiveLoans);
    }

    private static void AssertEquivalentCreation(CreateLoanResponse expected, CreateLoanResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.LoanId, actual.LoanId);
        Assert.Equal(expected.BookId, actual.BookId);
        Assert.Equal(expected.UserId, actual.UserId);
        AssertSamePersistedInstant(expected.LoanedAt, actual.LoanedAt);
        AssertSamePersistedInstant(expected.DueAt, actual.DueAt);
    }

    private static void AssertSamePersistedInstant(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.Equal(TimeSpan.Zero, expected.Offset);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.Equal(expected.UtcTicks / TimeSpan.TicksPerMicrosecond, actual.UtcTicks / TimeSpan.TicksPerMicrosecond);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}