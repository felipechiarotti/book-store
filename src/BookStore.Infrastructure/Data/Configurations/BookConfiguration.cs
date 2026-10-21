using BookStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Data.Configurations;

public sealed class BookConfiguration : BaseEntityConfiguration<Book>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");

        builder.Property(book => book.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(book => book.Author)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(book => book.Isbn)
            .IsRequired()
            .HasMaxLength(13);

        builder.Property(book => book.Quantity)
            .IsRequired();

        builder.HasIndex(book => book.Isbn)
            .IsUnique();
    }
}