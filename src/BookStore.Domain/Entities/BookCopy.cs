using BookStore.Domain.Enums;

namespace BookStore.Domain.Entities;

public class BookCopy : BaseEntity
{
    private BookCopy() { }
    
    public Guid BookId { get; private set; }
    public string InventoryCode {get; private set;} = string.Empty;
    public BookCopyStatus Status { get; private set; } = BookCopyStatus.AvailableForCirculation;
    
    public static BookCopy Create(Guid bookId, string inventoryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryCode);
        ArgumentNullException.ThrowIfNull(bookId);
        
        return new BookCopy
        {
            BookId = bookId,
            InventoryCode = inventoryCode
        };
    }

    public void Withdraw() =>
        Status = BookCopyStatus.Withdrawn;
}