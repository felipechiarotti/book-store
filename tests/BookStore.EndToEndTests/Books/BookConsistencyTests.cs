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

namespace BookStore.EndToEndTests.Books;

[Collection(nameof(BookStoreCollection))]
[Trait("Category", "EndToEnd")]
public sealed class BookConsistencyTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Update_WhenQuantityWouldExcludeActiveLoans_ShouldRejectAndAllowTheBoundary()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(
            new TestBookBuilder().WithQuantity(2).Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        using var created =
            await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "loan", actor, "loan",
                token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var rejected = await fixture.Secondary.UpdateQuantityAsync(book.Id, 0, actor, "invalid", token);

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await using (var rejectedContext = fixture.CreateDbContext())
        {
            Assert.Equal(2, (await rejectedContext.Books.AsNoTracking().SingleAsync(token)).Quantity);
            Assert.False(
                await rejectedContext.AuditEvents.AnyAsync(audit => audit.Action == AuditAction.Updated, token));
        }

        using var accepted = await fixture.Secondary.UpdateQuantityAsync(book.Id, 1, actor, "boundary", token);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var availability =
            await fixture.Primary.GetAsync<BookAvailabilityResponse>($"/books/{book.Id}/availability", token);
        Assert.Equal(1, availability.Quantity);
        Assert.Equal(1, availability.ActiveLoans);
        Assert.Equal(0, availability.AvailableQuantity);
        await using var context = fixture.CreateDbContext();
        var audit = await AuditAssertions.SingleAsync(context, nameof(Book), book.Id, AuditAction.Updated,
            actor, "boundary", token);
        using var details = JsonDocument.Parse(audit.Details);
        Assert.Equal(2, details.RootElement.GetProperty("Before").GetProperty("Quantity").GetInt32());
        Assert.Equal(1, details.RootElement.GetProperty("After").GetProperty("Quantity").GetInt32());
    }

    [Fact]
    public async Task Deactivate_WhenBookHasALoan_ShouldPreserveHistoryAndAllowReturn()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);
        using var created = await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "original",
            actor, "loan", token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var loan = await created.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        Assert.NotNull(loan);

        using var deactivated = await fixture.Secondary.DeactivateBookAsync(book.Id, actor, "deactivate", token);

        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        var beforeReturn = await fixture.Primary.GetAsync<LoanResponse>($"/loans/{loan.LoanId}", token);
        Assert.Equal(LoanStatus.Active, beforeReturn.Status);
        using var rejected =
            await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "new", actor, "new", token);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var returned = await fixture.Secondary.CloseLoanAsync(loan.LoanId, "return", actor, "return", token);
        Assert.Equal(HttpStatusCode.NoContent, returned.StatusCode);
        using var rejectedWithFreeCapacity = await fixture.Primary.CreateLoanAsync(
            new CreateLoanRequest(book.Id, user.Id), "after-return", actor, "after-return", token);
        Assert.Equal(HttpStatusCode.Conflict, rejectedWithFreeCapacity.StatusCode);
        var history =
            await fixture.Primary.GetAsync<PageResponse<LoanHistoryResponse>>($"/books/{book.Id}/history", token);
        var userHistory =
            await fixture.Secondary.GetAsync<PageResponse<LoanHistoryResponse>>($"/users/{user.Id}/loans", token);
        Assert.Equal(loan.LoanId, Assert.Single(history.Items).Id);
        Assert.Equal(nameof(LoanStatus.Returned), Assert.Single(history.Items).Status);
        Assert.Equal(Assert.Single(history.Items), Assert.Single(userHistory.Items));
        await using var context = fixture.CreateDbContext();
        var storedBook = await context.Books.AsNoTracking().SingleAsync(token);
        Assert.False(storedBook.IsActive);
        Assert.Equal(1, storedBook.Quantity);
        Assert.Equal(0, await context.Loans.CountAsync(loan => loan.Status == LoanStatus.Active, token));
        Assert.Single(await context.Loans.AsNoTracking().ToListAsync(token));
        Assert.Equal(1, await context.AuditEvents.CountAsync(audit =>
            audit.EntityName == nameof(Loan) && audit.Action == AuditAction.Created, token));
        var audit = await AuditAssertions.SingleAsync(context, nameof(Book), book.Id, AuditAction.Deactivated,
            actor, "deactivate", token);
        using var details = JsonDocument.Parse(audit.Details);
        Assert.True(details.RootElement.GetProperty("PreviousIsActive").GetBoolean());
        Assert.False(details.RootElement.GetProperty("IsActive").GetBoolean());
    }

    [Fact]
    public async Task Create_WhenIsbnHasEquivalentFormatting_ShouldPreserveOnlyTheFirstBook()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var request = new TestBookBuilder().WithIsbn("978-0-13-235088-4").Build();
        var book = await fixture.Primary.CreateBookAsync(request, actor, "original", token);
        using var duplicate = new HttpRequestMessage(HttpMethod.Post, "/books")
        {
            Content = JsonContent.Create(request with { Isbn = "9780132350884" })
        };
        duplicate.Headers.Add("X-User-Id", actor.ToString());

        using var response = await fixture.Secondary.HttpClient.SendAsync(duplicate, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var context = fixture.CreateDbContext();
        Assert.Equal(book.Id, Assert.Single(await context.Books.AsNoTracking().ToListAsync(token)).Id);
        Assert.Equal(1, await context.AuditEvents.CountAsync(token));
        var audit = await AuditAssertions.SingleAsync(context, nameof(Book), book.Id, AuditAction.Created,
            actor, "original", token);
        using var details = JsonDocument.Parse(audit.Details);
        Assert.Equal(request.Title, details.RootElement.GetProperty("Title").GetString());
        Assert.Equal("9780132350884", details.RootElement.GetProperty("Isbn").GetString());
        Assert.Equal(request.Author, details.RootElement.GetProperty("Author").GetString());
        Assert.Equal(1, details.RootElement.GetProperty("Quantity").GetInt32());
        Assert.True(details.RootElement.GetProperty("IsActive").GetBoolean());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}