using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class LoanClosureTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("return", LoanStatus.Returned, AuditAction.Returned)]
    [InlineData("cancel", LoanStatus.Cancelled, AuditAction.Cancelled)]
    public async Task Close_WhenRepeated_ShouldPreserveHistoryAndReleaseCapacityOnce(
        string action, LoanStatus status, AuditAction auditAction)
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        using var created = await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "loan",
            actor, "create", token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await created.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        Assert.NotNull(result);
        var original = await fixture.Primary.GetAsync<LoanResponse>($"/loans/{result.LoanId}", token);

        using var closed = await fixture.Secondary.CloseLoanAsync(result.LoanId, action, actor, "close", token);

        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        var after = await fixture.Primary.GetAsync<LoanResponse>($"/loans/{result.LoanId}", token);
        Assert.Equal(status, after.Status);
        Assert.NotNull(after.ClosedAt);
        Assert.Equal(TimeSpan.Zero, after.ClosedAt?.Offset);
        Assert.Equal(original.BookId, after.BookId);
        Assert.Equal(original.UserId, after.UserId);
        Assert.Equal(original.LoanedAt, after.LoanedAt);
        Assert.Equal(original.DueAt, after.DueAt);
        await using var beforeRepeat = fixture.CreateDbContext();
        var storedBeforeRepeat = await beforeRepeat.Loans.AsNoTracking().SingleAsync(token);

        using var repeated =
            await fixture.Primary.CloseLoanAsync(result.LoanId, action, Guid.NewGuid(), "repeat", token);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(after, await fixture.Secondary.GetAsync<LoanResponse>($"/loans/{result.LoanId}", token));
        var bookHistory = await fixture.Primary.GetAsync<PageResponse<LoanHistoryResponse>>(
            $"/books/{book.Id}/history", token);
        var userHistory = await fixture.Secondary.GetAsync<PageResponse<LoanHistoryResponse>>(
            $"/users/{user.Id}/loans", token);
        Assert.Equal(1, bookHistory.TotalCount);
        Assert.Equal(1, userHistory.TotalCount);
        Assert.Equal(result.LoanId, Assert.Single(bookHistory.Items).Id);
        Assert.Equal(status.ToString(), Assert.Single(bookHistory.Items).Status);
        Assert.Equal(Assert.Single(bookHistory.Items), Assert.Single(userHistory.Items));
        var availability =
            await fixture.Primary.GetAsync<BookAvailabilityResponse>($"/books/{book.Id}/availability", token);
        Assert.Equal(1, availability.Quantity);
        Assert.Equal(0, availability.ActiveLoans);
        Assert.Equal(1, availability.AvailableQuantity);

        await using var context = fixture.CreateDbContext();
        var stored = await context.Loans.AsNoTracking().SingleAsync(token);
        Assert.Equal(storedBeforeRepeat.ModifiedAt, stored.ModifiedAt);
        Assert.Equal("loan", stored.IdempotencyKey);
        Assert.Equal(2, await context.AuditEvents.CountAsync(audit => audit.EntityId == result.LoanId, token));
        var creation = await AuditAssertions.SingleAsync(context, nameof(Loan), result.LoanId, AuditAction.Created,
            actor, "create", token);
        using var creationDetails = JsonDocument.Parse(creation.Details);
        Assert.Equal(book.Id, creationDetails.RootElement.GetProperty("BookId").GetGuid());
        Assert.Equal(user.Id, creationDetails.RootElement.GetProperty("UserId").GetGuid());
        Assert.Equal(result.LoanedAt, creationDetails.RootElement.GetProperty("LoanedAt").GetDateTimeOffset());
        Assert.Equal(result.DueAt, creationDetails.RootElement.GetProperty("DueAt").GetDateTimeOffset());
        var audit = await AuditAssertions.SingleAsync(context, nameof(Loan), result.LoanId, auditAction,
            actor, "close", token);
        using var details = JsonDocument.Parse(audit.Details);
        Assert.Equal(book.Id, details.RootElement.GetProperty("BookId").GetGuid());
        Assert.Equal(user.Id, details.RootElement.GetProperty("UserId").GetGuid());
        Assert.Equal((int)LoanStatus.Active, details.RootElement.GetProperty("PreviousStatus").GetInt32());
        Assert.Equal((int)status, details.RootElement.GetProperty("Status").GetInt32());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}