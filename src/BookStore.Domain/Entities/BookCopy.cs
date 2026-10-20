using BookStore.Domain.Enums;
using BookStore.Domain.Interfaces;

namespace BookStore.Domain.Entities;

public class BookCopy : BaseEntity, ISoftDeletable
{
    private BookCopy()
    {
    }

    public Guid BookId { get; private set; }
    public string InventoryCode { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; } = false;

    public static BookCopy Create(Guid bookId, string inventoryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryCode);
        if (bookId == Guid.Empty)
            throw new ArgumentException("BookId inválido.", nameof(bookId));

        return new BookCopy
        {
            BookId = bookId,
            InventoryCode = inventoryCode
        };
    }
}