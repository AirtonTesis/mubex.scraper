using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Tests.Entities;

/// <summary>
/// Testes unitários para a entidade JobHistoryEntry
/// **Validates: Requirements 4.8**
/// </summary>
public class JobHistoryEntryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithJobIdAndStatus_ShouldCreateValidEntry()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var status = JobStatus.Active;

        // Act
        var entry = new JobHistoryEntry(jobId, status);

        // Assert
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(jobId, entry.JobId);
        Assert.Equal(JobStatus.Active, entry.Status);
        Assert.True(entry.Timestamp <= DateTime.UtcNow);
        Assert.True(entry.Timestamp >= DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var entry1 = new JobHistoryEntry(jobId, JobStatus.Pending);
        var entry2 = new JobHistoryEntry(jobId, JobStatus.Active);

        // Assert
        Assert.NotEqual(entry1.Id, entry2.Id);
    }

    [Fact]
    public void Constructor_ShouldSetTimestampToUtcNow()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var entry = new JobHistoryEntry(jobId, JobStatus.Pending);

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(entry.Timestamp >= beforeCreation);
        Assert.True(entry.Timestamp <= afterCreation);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Active)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    public void Constructor_WithDifferentStatuses_ShouldStoreCorrectStatus(JobStatus status)
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var entry = new JobHistoryEntry(jobId, status);

        // Assert
        Assert.Equal(status, entry.Status);
    }

    [Fact]
    public void DefaultConstructor_ShouldCreateEmptyEntry()
    {
        // Act
        var entry = new JobHistoryEntry();

        // Assert
        Assert.Equal(Guid.Empty, entry.Id);
        Assert.Equal(Guid.Empty, entry.JobId);
        Assert.Equal(default(JobStatus), entry.Status);
        Assert.Equal(default(DateTime), entry.Timestamp);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_ShouldBeSettableForEFCore()
    {
        // Arrange
        var entry = new JobHistoryEntry();
        var id = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var status = JobStatus.Completed;
        var timestamp = DateTime.UtcNow;

        // Act
        entry.Id = id;
        entry.JobId = jobId;
        entry.Status = status;
        entry.Timestamp = timestamp;

        // Assert
        Assert.Equal(id, entry.Id);
        Assert.Equal(jobId, entry.JobId);
        Assert.Equal(JobStatus.Completed, entry.Status);
        Assert.Equal(timestamp, entry.Timestamp);
    }

    #endregion

    #region Multiple Entries for Same Job Tests

    [Fact]
    public void MultipleEntries_ForSameJob_ShouldHaveDifferentIds()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var entry1 = new JobHistoryEntry(jobId, JobStatus.Pending);
        var entry2 = new JobHistoryEntry(jobId, JobStatus.Active);
        var entry3 = new JobHistoryEntry(jobId, JobStatus.Completed);

        // Assert
        Assert.NotEqual(entry1.Id, entry2.Id);
        Assert.NotEqual(entry1.Id, entry3.Id);
        Assert.NotEqual(entry2.Id, entry3.Id);
        Assert.Equal(jobId, entry1.JobId);
        Assert.Equal(jobId, entry2.JobId);
        Assert.Equal(jobId, entry3.JobId);
    }

    [Fact]
    public void MultipleEntries_ShouldHaveSequentialTimestamps()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var entry1 = new JobHistoryEntry(jobId, JobStatus.Pending);
        System.Threading.Thread.Sleep(10); // Pequeno delay para garantir timestamps diferentes
        var entry2 = new JobHistoryEntry(jobId, JobStatus.Active);
        System.Threading.Thread.Sleep(10);
        var entry3 = new JobHistoryEntry(jobId, JobStatus.Completed);

        // Assert
        Assert.True(entry2.Timestamp >= entry1.Timestamp);
        Assert.True(entry3.Timestamp >= entry2.Timestamp);
    }

    #endregion
}
