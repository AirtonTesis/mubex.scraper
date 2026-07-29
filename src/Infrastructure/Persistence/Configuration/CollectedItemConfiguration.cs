using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public class CollectedItemConfiguration : IEntityTypeConfiguration<CollectedItem>
{
    public void Configure(EntityTypeBuilder<CollectedItem> builder)
    {
        builder.ToTable("CollectedItems");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.JobId)
            .IsRequired();

        builder.Property(ci => ci.Keyword)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(ci => ci.Domain)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ci => ci.Position)
            .IsRequired();

        builder.Property(ci => ci.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(ci => ci.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(ci => ci.Snippet)
            .HasMaxLength(2000)
            .IsRequired();

        // Índice para consultas por Job
        builder.HasIndex(ci => ci.JobId);

        // Índice composto para consultas por Job + Keyword
        builder.HasIndex(ci => new { ci.JobId, ci.Keyword });
    }
}
