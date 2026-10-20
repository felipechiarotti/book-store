namespace BookStore.Domain.Entities;

public class Book : BaseEntity
{
    private Book() { }
    
    public string Title { get; private set; } = string.Empty;
    public string Isbn { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static Book Create(string title, string isbn, string author)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        
        return new Book
        {
            Title = title,
            Isbn = isbn,
            Author = author
        };
    }

    public void UpdateDetails(string title, string author)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        Title = title;
        Author = author;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}