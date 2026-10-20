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

        builder.Property(copy => copy.Status)
            .IsRequired();

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(copy => copy.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}