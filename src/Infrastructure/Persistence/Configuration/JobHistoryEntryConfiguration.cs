using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public class JobHistoryEntryConfiguration : IEntityTypeConfiguration<JobHistoryEntry>
{
    public void Configure(EntityTypeBuilder<JobHistoryEntry> builder)
    {
        builder.ToTable("JobHistoryEntries");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.JobId)
            .IsRequired();

        builder.Property(h => h.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(h => h.Timestamp)
            .IsRequired();

        builder.HasIndex(h => h.JobId);
    }
}
