using System.Net;
using BookStore.Application.Common.Models;
using BookStore.EndToEndTests.Infrastructure;

namespace BookStore.EndToEndTests.Books;

[Collection(nameof(BookStoreCollection))]
[Trait("Category", "EndToEnd")]
public sealed class CatalogCacheTests(BookStoreFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Get_WhenQuantityChangesAfterCacheFill_ShouldReturnUpdatedCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        var book = await fixture.Primary.CreateBookAsync(new TestBookBuilder().Build(), actor, "book", token);
        var before = await fixture.Primary.GetAsync<PageResponse<BookResponse>>("/books?page=1&pageSize=20", token);
        Assert.Equal(1, Assert.Single(before.Items).Quantity);
        var originalGeneration = await fixture.GetCatalogGenerationAsync(token);
        Assert.NotNull(originalGeneration);
        Assert.True(await fixture.CatalogPageExistsAsync(originalGeneration, 1, 20, token));

        using var updated = await fixture.Secondary.UpdateQuantityAsync(book.Id, 2, actor, "update", token);

        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        var after = await fixture.Primary.GetAsync<PageResponse<BookResponse>>("/books?page=1&pageSize=20", token);
        Assert.Equal(1, after.TotalCount);
        var item = Assert.Single(after.Items);
        Assert.Equal(book.Id, item.Id);
        Assert.Equal(2, item.Quantity);
        var currentGeneration = await fixture.GetCatalogGenerationAsync(token);
        Assert.NotNull(currentGeneration);
        Assert.NotEqual(originalGeneration, currentGeneration);
        Assert.True(await fixture.CatalogPageExistsAsync(currentGeneration, 1, 20, token));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}