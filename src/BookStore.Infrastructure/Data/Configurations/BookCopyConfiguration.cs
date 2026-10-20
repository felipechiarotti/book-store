using BookStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Data.Configurations;

public sealed class BookCopyConfiguration
    : BaseEntityConfiguration<BookCopy>
{
    protected override void ConfigureEntity(
        EntityTypeBuilder<BookCopy> builder)
    {
        builder.ToTable("book_copies");

        builder.Property(copy => copy.InventoryCode)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(copy => copy.InventoryCode)
            .IsUnique();

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasQueryFilter(c => !c.IsDeleted);
        
        builder.HasOne<Book>()
            .WithMany(book => book.Copies)
            .HasForeignKey(copy => copy.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}