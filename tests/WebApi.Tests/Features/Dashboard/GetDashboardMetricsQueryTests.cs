using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using WebApi.Features.Dashboard;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.Dashboard;

/// <summary>
/// Testes de integração para GetDashboardMetricsQuery.
/// **Property 17: Dashboard Metrics Calculation Accuracy**
/// **Property 18: Custom Date Range Filtering**
/// **Valida: Requirements 7.1, 7.7**
/// </summary>
public class GetDashboardMetricsQueryTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public GetDashboardMetricsQueryTests()
    {
        _context = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que GetDashboardMetrics calcula métricas corretamente.
    /// **Validates: Requirement 7.1**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCalculateMetricsCorrectly()
    {
        // Arrange - Create jobs with different statuses
        var userId = Guid.NewGuid();
        var searchList = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        _context.SearchLists.Add(searchList.Value);
        await _context.SaveChangesAsync();

        // Create 4 jobs: 2 completed, 1 failed, 1 active
        var job1 = Job.Create(searchList.Value.Id);
        job1.Start();
        job1.Complete();

        var job2 = Job.Create(searchList.Value.Id);
        job2.Start();
        job2.Complete();

        var job3 = Job.Create(searchList.Value.Id);
        job3.Start();
        job3.Fail("Error");

        var job4 = Job.Create(searchList.Value.Id);
        job4.Start();

        _context.Jobs.AddRange(job1, job2, job3, job4);
        await _context.SaveChangesAsync();

        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);
        var query = new GetDashboardMetricsQuery(startDate, endDate);
        var handler = new GetDashboardMetricsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.TotalSearches);
        Assert.Equal(50.0, result.SuccessRate); // 2/4 = 50%
        Assert.Equal(25.0, result.FailureRate); // 1/4 = 25%
        Assert.Equal(1, result.ActiveExecutions);
    }

    /// <summary>
    /// Testa que GetDashboardMetrics filtra por intervalo de datas.
    /// **Validates: Requirement 7.7**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldFilterByDateRange()
    {
        // Arrange - Create jobs with different dates
        var userId = Guid.NewGuid();
        var searchList = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        _context.SearchLists.Add(searchList.Value);
        await _context.SaveChangesAsync();

        // Create a job within the date range
        var recentJob = Job.Create(searchList.Value.Id);
        recentJob.Start();
        recentJob.Complete();
        _context.Jobs.Add(recentJob);
        await _context.SaveChangesAsync();

        // Query only from now onwards - should include the recent job
        var startDate = recentJob.CreatedAt.AddSeconds(-1);
        var endDate = DateTime.UtcNow.AddDays(1);
        var query = new GetDashboardMetricsQuery(startDate, endDate);
        var handler = new GetDashboardMetricsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Only recent job should be counted
        Assert.Equal(1, result.TotalSearches);
        Assert.Equal(100.0, result.SuccessRate);
    }

    /// <summary>
    /// Testa que GetDashboardMetrics retorna métricas zeradas quando não há jobs.
    /// **Validates: Requirement 7.1**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnZeroMetrics_WhenNoJobs()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);
        var query = new GetDashboardMetricsQuery(startDate, endDate);
        var handler = new GetDashboardMetricsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(0, result.TotalSearches);
        Assert.Equal(0, result.SuccessRate);
        Assert.Equal(0, result.FailureRate);
        Assert.Equal(0, result.ActiveExecutions);
    }
}
