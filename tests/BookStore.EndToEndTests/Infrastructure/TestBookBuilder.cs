using BookStore.Application.Features.Books.Commands;

namespace BookStore.EndToEndTests.Infrastructure;

internal sealed class TestBookBuilder
{
    private int _quantity = 1;
    private string _isbn = "9780132350884";

    public TestBookBuilder WithQuantity(int quantity)
    {
        _quantity = quantity;
        return this;
    }

    public TestBookBuilder WithIsbn(string isbn)
    {
        _isbn = isbn;
        return this;
    }

    public CreateBookCommand Build()
    {
        return new CreateBookCommand("Clean Code", _isbn, "Robert Martin", _quantity);
    }
}