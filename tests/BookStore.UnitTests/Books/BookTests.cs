using BookStore.Domain.Entities;

namespace BookStore.UnitTests.Books;

[Trait("Category", "Unit")]
public sealed class BookTests
{
    [Fact]
    public void Create_WhenQuantityIsNegative_ShouldRejectBook()
    {
        var create = () => Book.Create("Clean Code", "9780132350884", "Robert Martin", -1);

        Assert.Throws<ArgumentOutOfRangeException>("quantity", create);
    }

    [Fact]
    public void Create_WhenQuantityIsZero_ShouldAcceptBook()
    {
        var book = Book.Create("Clean Code", "9780132350884", "Robert Martin", 0);

        Assert.Equal(0, book.Quantity);
    }

    [Fact]
    public void UpdateDetails_WhenQuantityIsNegative_ShouldPreserveBook()
    {
        var book = Book.Create("Clean Code", "9780132350884", "Robert Martin", 2);

        var update = () => book.UpdateDetails("Changed title", "Changed author", -1);

        Assert.Throws<ArgumentOutOfRangeException>("quantity", update);
        Assert.Equal(2, book.Quantity);
        Assert.Equal("Clean Code", book.Title);
        Assert.Equal("Robert Martin", book.Author);
    }

    [Fact]
    public void UpdateDetails_WhenQuantityIsZero_ShouldAcceptQuantity()
    {
        var book = Book.Create("Clean Code", "9780132350884", "Robert Martin", 2);

        book.UpdateDetails("Clean Code", "Robert Martin", 0);

        Assert.Equal(0, book.Quantity);
    }
}