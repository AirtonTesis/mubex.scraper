using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Tests.TestHelpers;

/// <summary>
/// Factory for creating in-memory ApplicationDbContext instances for testing.
/// Each test gets a fresh database to ensure isolation.
/// </summary>
public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}
