using BookStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : BaseEntityConfiguration<User>
{
    protected override void ConfigureEntity(
        EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.Property(user => user.Name)
            .IsRequired()
            .HasMaxLength(200);
    }
}