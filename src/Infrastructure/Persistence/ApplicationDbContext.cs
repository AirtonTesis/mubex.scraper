using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SearchList> SearchLists => Set<SearchList>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobHistoryEntry> JobHistoryEntries => Set<JobHistoryEntry>();
    public DbSet<CollectedItem> CollectedItems => Set<CollectedItem>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Atualizar timestamps automaticamente via reflection (propriedades têm setter protected)
        foreach (var entry in ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var type = typeof(BaseEntity);
            type.GetProperty("UpdatedAt")!.SetValue(entry.Entity, now);

            if (entry.State == EntityState.Added)
                type.GetProperty("CreatedAt")!.SetValue(entry.Entity, now);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
