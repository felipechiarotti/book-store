using BookStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Data.Configurations;

public sealed class AuditEventConfiguration
    : BaseEntityConfiguration<AuditEvent>
{
    protected override void ConfigureEntity(
        EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.Property(audit => audit.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(audit => audit.Action)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(audit => audit.CorrelationId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(audit => audit.Details)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasIndex(audit => new
        {
            audit.EntityName,
            audit.EntityId,
            audit.CreatedAt
        });
    }
}
