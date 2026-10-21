using System.Net;
using System.Net.Http.Json;
using BookStore.Api.Modules.Loans;
using BookStore.Application.Common.Models;
using BookStore.Application.Features.Loans.Commands;
using BookStore.Domain.Entities;
using BookStore.Domain.Enums;
using BookStore.EndToEndTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.EndToEndTests.Loans;

[Collection(nameof(BookStoreCollection))]
[Trait("Category", "EndToEnd")]
public sealed class LoanAtomicityTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Create_WhenAuditInsertFails_ShouldRollbackAndAllowRetry()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var user = await fixture.Primary.CreateUserAsync("Library user", token);

        await using (var failure = await fixture.FailLoanAuditAsync(token))
        {
            using var failed = await fixture.Primary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "retry",
                actor, "failure", token);

            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            var problem = await failed.Content.ReadFromJsonAsync<ProblemDetails>(token);
            Assert.NotNull(problem);
            Assert.Equal(500, problem.Status);
            Assert.DoesNotContain("audit-insert-failure", problem.Detail ?? string.Empty);
            await using var context = fixture.CreateDbContext();
            Assert.False(await context.Loans.AnyAsync(token));
            Assert.False(await context.AuditEvents.AnyAsync(audit => audit.EntityName == nameof(Loan), token));
            var availability = await fixture.Primary.GetAsync<BookAvailabilityResponse>(
                $"/books/{book.Id}/availability", token);
            Assert.Equal(1, availability.AvailableQuantity);
        }

        using var retried = await fixture.Secondary.CreateLoanAsync(new CreateLoanRequest(book.Id, user.Id), "retry",
            actor, "retry", token);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        var result = await retried.Content.ReadFromJsonAsync<CreateLoanResponse>(token);
        Assert.NotNull(result);
        await using var confirmed = fixture.CreateDbContext();
        Assert.Single(await confirmed.Loans.AsNoTracking().ToListAsync(token));
        await AuditAssertions.SingleAsync(confirmed, nameof(Loan), result.LoanId, AuditAction.Created, actor, "retry",
            token);
        Assert.Equal(1, await confirmed.AuditEvents.CountAsync(audit => audit.EntityName == nameof(Loan), token));
        var finalAvailability = await fixture.Secondary.GetAsync<BookAvailabilityResponse>(
            $"/books/{book.Id}/availability", token);
        Assert.Equal(0, finalAvailability.AvailableQuantity);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}