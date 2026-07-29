using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.SearchListId)
            .IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(j => j.StartedAt);

        builder.Property(j => j.CompletedAt);

        builder.Property(j => j.RetryCount)
            .HasDefaultValue(0);

        builder.Property(j => j.ItemsCollected)
            .HasDefaultValue(0);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .IsRequired();

        builder.HasIndex(j => j.SearchListId);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CreatedAt);

        builder.HasOne(j => j.SearchList)
            .WithMany()
            .HasForeignKey(j => j.SearchListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.History)
            .WithOne()
            .HasForeignKey(h => h.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
