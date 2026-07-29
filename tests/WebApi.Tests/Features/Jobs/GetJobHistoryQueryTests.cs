using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WebApi.Features.Jobs;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.Jobs;

/// <summary>
/// Testes de integração para GetJobHistoryQuery.
/// **Property 11: Job History Chronological Ordering**
/// **Valida: Requirements 4.10**
/// </summary>
public class GetJobHistoryQueryTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public GetJobHistoryQueryTests()
    {
        _context = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que GetJobHistory retorna entradas ordenadas cronologicamente.
    /// **Validates: Requirement 4.10**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnEntries_InChronologicalOrder()
    {
        // Arrange - Create a job with history entries
        var userId = Guid.NewGuid();
        var searchList = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        _context.SearchLists.Add(searchList.Value);
        await _context.SaveChangesAsync();

        var job = Job.Create(searchList.Value.Id);
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        // Manually add history entries to simulate state transitions
        // (Job.Create() already added a Pending entry via the History collection)
        var entry1 = new JobHistoryEntry(job.Id, JobStatus.Active) { Timestamp = DateTime.UtcNow.AddSeconds(1) };
        var entry2 = new JobHistoryEntry(job.Id, JobStatus.Completed) { Timestamp = DateTime.UtcNow.AddSeconds(2) };

        _context.JobHistoryEntries.AddRange(entry1, entry2);
        await _context.SaveChangesAsync();

        var query = new GetJobHistoryQuery(job.Id);
        var handler = new GetJobHistoryHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Pending (from Create) + Active + Completed = 3
        Assert.Equal(3, result.Count);

        // Verify chronological ordering
        for (int i = 1; i < result.Count; i++)
        {
            Assert.True(result[i].Timestamp >= result[i - 1].Timestamp);
        }
    }

    /// <summary>
    /// Testa que GetJobHistory retorna apenas entradas do job especificado.
    /// **Validates: Requirements 4.8**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnOnlySpecifiedJobHistory()
    {
        // Arrange - Create two jobs with history
        var userId = Guid.NewGuid();
        var searchList = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        _context.SearchLists.Add(searchList.Value);
        await _context.SaveChangesAsync();

        var job1 = Job.Create(searchList.Value.Id);
        var job2 = Job.Create(searchList.Value.Id);
        _context.Jobs.AddRange(job1, job2);
        await _context.SaveChangesAsync();

        // Job.Create() already adds a Pending entry for each job
        var query = new GetJobHistoryQuery(job1.Id);
        var handler = new GetJobHistoryHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Each job has 1 entry from Create(), so query job1 returns only its entry
        Assert.Single(result);
        Assert.Equal(job1.Id, result.First().JobId);
    }

    /// <summary>
    /// Testa que GetJobHistory retorna lista vazia quando job não tem histórico.
    /// **Validates: Requirements 4.8**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoHistory()
    {
        // Arrange
        var query = new GetJobHistoryQuery(Guid.NewGuid());
        var handler = new GetJobHistoryHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
