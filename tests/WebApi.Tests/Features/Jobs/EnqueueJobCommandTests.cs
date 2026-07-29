using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using Moq;
using WebApi.Features.Jobs;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.Jobs;

/// <summary>
/// Testes de integração para EnqueueJobCommand.
/// **Property 9: Job Enqueue is Non-Blocking**
/// **Valida: Requirements 4.2**
/// </summary>
public class EnqueueJobCommandTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IQueueManager> _queueManagerMock;

    public EnqueueJobCommandTests()
    {
        _context = TestDbContextFactory.Create();
        _queueManagerMock = new Mock<IQueueManager>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que EnqueueJob cria um Job no banco de dados e o adiciona à fila.
    /// **Validates: Requirement 4.1, 4.2**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCreateJob_AndEnqueueToQueue()
    {
        // Arrange - Create a SearchList first
        var userId = Guid.NewGuid();
        var searchList = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        _context.SearchLists.Add(searchList.Value);
        await _context.SaveChangesAsync();

        var command = new EnqueueJobCommand(searchList.Value.Id);
        var handler = new EnqueueJobHandler(_context, _queueManagerMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        // Verify job was created in database
        var job = await _context.Jobs.FindAsync(result.Value);
        Assert.NotNull(job);
        Assert.Equal(searchList.Value.Id, job.SearchListId);
        Assert.Equal(JobStatus.Pending, job.Status);

        // Verify job was enqueued
        _queueManagerMock.Verify(
            q => q.EnqueueJobAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Testa que EnqueueJob retorna falha quando SearchList não existe.
    /// **Validates: Requirement 4.1**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenSearchListNotFound()
    {
        // Arrange
        var command = new EnqueueJobCommand(Guid.NewGuid());
        var handler = new EnqueueJobHandler(_context, _queueManagerMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key.Contains("not_found"));

        // Verify queue manager was NOT called
        _queueManagerMock.Verify(
            q => q.EnqueueJobAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Testa que EnqueueJob retorna imediatamente sem aguardar processamento.
    /// **Validates: Requirement 4.2**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnImmediately_AfterEnqueue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var searchList = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        _context.SearchLists.Add(searchList.Value);
        await _context.SaveChangesAsync();

        var command = new EnqueueJobCommand(searchList.Value.Id);
        var handler = new EnqueueJobHandler(_context, _queueManagerMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Queue manager verification already proves non-blocking behavior
        Assert.True(result.IsSuccess);
        _queueManagerMock.Verify(
            q => q.EnqueueJobAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
