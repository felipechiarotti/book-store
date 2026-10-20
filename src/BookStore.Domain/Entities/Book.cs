using BookStore.Domain.Rules;

namespace BookStore.Domain.Entities;

public class Book : BaseEntity
{
    private Book()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Isbn { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public int Quantity { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;


    public static Book Create(string title, string isbn, string author, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);

        return new Book
        {
            Title = title.Trim(),
            Isbn = IsbnRules.Normalize(isbn),
            Quantity =  quantity,
            Author = author.Trim()
        };
    }

    public void UpdateDetails(string title, string author, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        Title = title.Trim();
        Author = author.Trim();
        Quantity = quantity;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}