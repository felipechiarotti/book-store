using BookStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Data.Configurations;

public sealed class LoanConfiguration : BaseEntityConfiguration<Loan>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");

        builder.Property(loan => loan.LoanedAt)
            .IsRequired();

        builder.Property(loan => loan.DueAt)
            .IsRequired();

        builder.Property(loan => loan.Status)
            .IsRequired();

        builder.Property(loan => loan.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(loan => loan.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(loan => loan.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(loan => new { loan.BookId, loan.Status });

        builder.HasIndex(loan => loan.UserId);

        builder.HasIndex(loan => loan.IdempotencyKey)
            .IsUnique();
    }
}