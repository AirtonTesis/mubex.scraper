using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Tests.Entities;

/// <summary>
/// Testes unitários para a entidade Job com máquina de estados
/// **Validates: Requirements 4.3, 4.4, 4.5, 4.6, 4.7, 4.8**
/// </summary>
public class JobTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_WithValidSearchListId_ShouldReturnJobInPendingState()
    {
        // Arrange
        var searchListId = Guid.NewGuid();

        // Act
        var job = Job.Create(searchListId);

        // Assert
        Assert.NotNull(job);
        Assert.Equal(searchListId, job.SearchListId);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Equal(0, job.RetryCount);
        Assert.Null(job.ErrorMessage);
        Assert.NotEqual(Guid.Empty, job.Id);
    }

    [Fact]
    public void Create_ShouldAddInitialHistoryEntry()
    {
        // Arrange
        var searchListId = Guid.NewGuid();

        // Act
        var job = Job.Create(searchListId);

        // Assert
        Assert.Single(job.History);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(job.Id, job.History[0].JobId);
        Assert.True(job.History[0].Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_ShouldInitializeTimestamps()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        var searchListId = Guid.NewGuid();

        // Act
        var job = Job.Create(searchListId);

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(job.CreatedAt >= beforeCreation);
        Assert.True(job.CreatedAt <= afterCreation);
        Assert.True(job.UpdatedAt >= beforeCreation);
        Assert.True(job.UpdatedAt <= afterCreation);
    }

    #endregion

    #region Start Method Tests

    [Fact]
    public void Start_FromPendingState_ShouldTransitionToActive()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.Start();

        // Assert
        Assert.Equal(JobStatus.Active, job.Status);
        Assert.NotNull(job.StartedAt);
        Assert.True(job.StartedAt <= DateTime.UtcNow);
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Start_FromPendingState_ShouldAddHistoryEntry()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();

        // Assert
        Assert.Equal(2, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
    }

    [Fact]
    public void Start_FromPausedState_ShouldTransitionToActive()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();

        // Act
        job.Start();

        // Assert
        Assert.Equal(JobStatus.Active, job.Status);
    }

    [Fact]
    public void Start_FromPausedState_ShouldNotChangeStartedAt()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        var originalStartedAt = job.StartedAt;
        job.Pause();
        System.Threading.Thread.Sleep(10);

        // Act
        job.Start();

        // Assert
        Assert.Equal(originalStartedAt, job.StartedAt);
    }

    [Fact]
    public void Start_FromActiveState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Start());
        Assert.Contains("Cannot start job in Active status", exception.Message);
    }

    [Fact]
    public void Start_FromCompletedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Complete();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Start());
        Assert.Contains("Cannot start job in Completed status", exception.Message);
    }

    [Fact]
    public void Start_FromFailedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Fail("Test error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Start());
        Assert.Contains("Cannot start job in Failed status", exception.Message);
    }

    #endregion

    #region Pause Method Tests

    [Fact]
    public void Pause_FromActiveState_ShouldTransitionToPaused()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.Pause();

        // Assert
        Assert.Equal(JobStatus.Paused, job.Status);
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Pause_FromActiveState_ShouldAddHistoryEntry()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act
        job.Pause();

        // Assert
        Assert.Equal(3, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Paused, job.History[2].Status);
    }

    [Fact]
    public void Pause_FromPendingState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Pause());
        Assert.Contains("Cannot pause job in Pending status", exception.Message);
    }

    [Fact]
    public void Pause_FromPausedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Pause());
        Assert.Contains("Cannot pause job in Paused status", exception.Message);
    }

    [Fact]
    public void Pause_FromCompletedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Complete();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Pause());
        Assert.Contains("Cannot pause job in Completed status", exception.Message);
    }

    [Fact]
    public void Pause_FromFailedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Fail("Test error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Pause());
        Assert.Contains("Cannot pause job in Failed status", exception.Message);
    }

    #endregion

    #region Resume Method Tests

    [Fact]
    public void Resume_FromPausedState_ShouldTransitionToActive()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.Resume();

        // Assert
        Assert.Equal(JobStatus.Active, job.Status);
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Resume_FromPausedState_ShouldAddHistoryEntry()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();

        // Act
        job.Resume();

        // Assert
        Assert.Equal(4, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Paused, job.History[2].Status);
        Assert.Equal(JobStatus.Active, job.History[3].Status);
    }

    [Fact]
    public void Resume_FromPendingState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Resume());
        Assert.Contains("Cannot resume job in Pending status", exception.Message);
    }

    [Fact]
    public void Resume_FromActiveState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Resume());
        Assert.Contains("Cannot resume job in Active status", exception.Message);
    }

    [Fact]
    public void Resume_FromCompletedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Complete();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Resume());
        Assert.Contains("Cannot resume job in Completed status", exception.Message);
    }

    [Fact]
    public void Resume_FromFailedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Fail("Test error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Resume());
        Assert.Contains("Cannot resume job in Failed status", exception.Message);
    }

    #endregion

    #region Complete Method Tests

    [Fact]
    public void Complete_FromActiveState_ShouldTransitionToCompleted()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.Complete();

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.True(job.CompletedAt <= DateTime.UtcNow);
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Complete_FromActiveState_ShouldAddHistoryEntry()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act
        job.Complete();

        // Assert
        Assert.Equal(3, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Completed, job.History[2].Status);
    }

    [Fact]
    public void Complete_FromPendingState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Complete());
        Assert.Contains("Cannot complete job in Pending status", exception.Message);
    }

    [Fact]
    public void Complete_FromPausedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Complete());
        Assert.Contains("Cannot complete job in Paused status", exception.Message);
    }

    [Fact]
    public void Complete_FromCompletedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Complete();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Complete());
        Assert.Contains("Cannot complete job in Completed status", exception.Message);
    }

    [Fact]
    public void Complete_FromFailedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Fail("Test error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Complete());
        Assert.Contains("Cannot complete job in Failed status", exception.Message);
    }

    #endregion

    #region Fail Method Tests

    [Fact]
    public void Fail_FromPendingState_ShouldTransitionToFailed()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        var errorMessage = "Failed to connect to proxy";
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.Fail(errorMessage);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(errorMessage, job.ErrorMessage);
        Assert.NotNull(job.CompletedAt);
        Assert.True(job.CompletedAt <= DateTime.UtcNow);
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Fail_FromActiveState_ShouldTransitionToFailed()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        var errorMessage = "CAPTCHA detected after 3 retries";

        // Act
        job.Fail(errorMessage);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(errorMessage, job.ErrorMessage);
    }

    [Fact]
    public void Fail_FromPausedState_ShouldTransitionToFailed()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();
        var errorMessage = "Job timeout exceeded";

        // Act
        job.Fail(errorMessage);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(errorMessage, job.ErrorMessage);
    }

    [Fact]
    public void Fail_ShouldAddHistoryEntry()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act
        job.Fail("Test error");

        // Assert
        Assert.Equal(3, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Failed, job.History[2].Status);
    }

    [Fact]
    public void Fail_FromCompletedState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Complete();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => job.Fail("Test error"));
        Assert.Contains("Cannot fail a job that is already completed", exception.Message);
    }

    [Fact]
    public void Fail_WithEmptyMessage_ShouldStoreEmptyMessage()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Fail(string.Empty);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(string.Empty, job.ErrorMessage);
    }

    #endregion

    #region IncrementRetry Method Tests

    [Fact]
    public void IncrementRetry_ShouldIncreaseRetryCount()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        Assert.Equal(0, job.RetryCount);

        // Act
        job.IncrementRetry();

        // Assert
        Assert.Equal(1, job.RetryCount);
    }

    [Fact]
    public void IncrementRetry_ShouldUpdateTimestamp()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.IncrementRetry();

        // Assert
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void IncrementRetry_MultipleTimes_ShouldAccumulate()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.IncrementRetry();
        job.IncrementRetry();
        job.IncrementRetry();

        // Assert
        Assert.Equal(3, job.RetryCount);
    }

    #endregion

    #region State Machine Complex Scenarios

    [Fact]
    public void StateMachine_PendingToActiveToCompleted_ShouldHaveCompleteHistory()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        job.Complete();

        // Assert
        Assert.Equal(3, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Completed, job.History[2].Status);
    }

    [Fact]
    public void StateMachine_PendingToActiveToPausedToActiveToCompleted_ShouldHaveCompleteHistory()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        job.Pause();
        job.Resume();
        job.Complete();

        // Assert
        Assert.Equal(5, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Paused, job.History[2].Status);
        Assert.Equal(JobStatus.Active, job.History[3].Status);
        Assert.Equal(JobStatus.Completed, job.History[4].Status);
    }

    [Fact]
    public void StateMachine_MultiplePauseResumeCycles_ShouldTrackAllTransitions()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        job.Pause();
        job.Resume();
        job.Pause();
        job.Resume();
        job.Complete();

        // Assert
        Assert.Equal(7, job.History.Count);
        Assert.Equal(JobStatus.Pending, job.History[0].Status);
        Assert.Equal(JobStatus.Active, job.History[1].Status);
        Assert.Equal(JobStatus.Paused, job.History[2].Status);
        Assert.Equal(JobStatus.Active, job.History[3].Status);
        Assert.Equal(JobStatus.Paused, job.History[4].Status);
        Assert.Equal(JobStatus.Active, job.History[5].Status);
        Assert.Equal(JobStatus.Completed, job.History[6].Status);
    }

    [Fact]
    public void StateMachine_WithRetries_ShouldTrackRetryCountIndependently()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        job.IncrementRetry();
        job.Pause();
        job.IncrementRetry();
        job.Resume();
        job.IncrementRetry();
        job.Complete();

        // Assert
        Assert.Equal(3, job.RetryCount);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(5, job.History.Count); // Pending, Active, Paused, Active, Completed
    }

    [Fact]
    public void StateMachine_FailAfterMultipleRetries_ShouldPreserveRetryCount()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        job.IncrementRetry();
        job.IncrementRetry();
        job.IncrementRetry();
        job.Fail("Max retries exceeded");

        // Assert
        Assert.Equal(3, job.RetryCount);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("Max retries exceeded", job.ErrorMessage);
    }

    #endregion

    #region BaseEntity Integration Tests

    [Fact]
    public void Job_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var job1 = Job.Create(Guid.NewGuid());
        var job2 = Job.Create(Guid.NewGuid());

        // Assert
        Assert.NotEqual(job1.Id, job2.Id);
    }

    [Fact]
    public void Job_ShouldHaveCreatedAtTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var job = Job.Create(Guid.NewGuid());

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(job.CreatedAt >= beforeCreation);
        Assert.True(job.CreatedAt <= afterCreation);
    }

    [Fact]
    public void Job_CreatedAtShouldNotChangeOnUpdate()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        var originalCreatedAt = job.CreatedAt;

        // Act
        job.Start();
        job.Pause();
        job.Resume();
        job.Complete();

        // Assert
        Assert.Equal(originalCreatedAt, job.CreatedAt);
    }

    [Fact]
    public void Job_UpdatedAtShouldChangeOnStateTransitions()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        var originalUpdatedAt = job.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        job.Start();

        // Assert
        Assert.True(job.UpdatedAt > originalUpdatedAt);
    }

    #endregion

    #region History Tracking Tests

    [Fact]
    public void History_ShouldContainJobIdInAllEntries()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();
        job.Resume();

        // Act & Assert
        foreach (var entry in job.History)
        {
            Assert.Equal(job.Id, entry.JobId);
        }
    }

    [Fact]
    public void History_ShouldHaveAscendingTimestamps()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        System.Threading.Thread.Sleep(5);
        job.Pause();
        System.Threading.Thread.Sleep(5);
        job.Resume();

        // Assert
        for (int i = 1; i < job.History.Count; i++)
        {
            Assert.True(job.History[i].Timestamp >= job.History[i - 1].Timestamp,
                $"History entry {i} timestamp should be >= entry {i - 1}");
        }
    }

    [Fact]
    public void History_EntriesShouldHaveUniqueIds()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();
        job.Pause();
        job.Resume();

        // Act
        var historyIds = job.History.Select(h => h.Id).ToList();

        // Assert
        Assert.Equal(historyIds.Count, historyIds.Distinct().Count());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithEmptyGuid_ShouldStillCreateJob()
    {
        // Arrange & Act
        var job = Job.Create(Guid.Empty);

        // Assert
        Assert.NotNull(job);
        Assert.Equal(Guid.Empty, job.SearchListId);
        Assert.Equal(JobStatus.Pending, job.Status);
    }

    [Fact]
    public void StartedAt_ShouldBeSetOnlyOnFirstStart()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Start();
        var firstStartedAt = job.StartedAt;
        job.Pause();
        System.Threading.Thread.Sleep(10);
        job.Start();

        // Assert
        Assert.Equal(firstStartedAt, job.StartedAt);
    }

    [Fact]
    public void CompletedAt_ShouldBeSetWhenCompleted()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act
        var beforeComplete = DateTime.UtcNow;
        job.Complete();
        var afterComplete = DateTime.UtcNow;

        // Assert
        Assert.NotNull(job.CompletedAt);
        Assert.True(job.CompletedAt >= beforeComplete);
        Assert.True(job.CompletedAt <= afterComplete);
    }

    [Fact]
    public void CompletedAt_ShouldBeSetWhenFailed()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());
        job.Start();

        // Act
        var beforeFail = DateTime.UtcNow;
        job.Fail("Test error");
        var afterFail = DateTime.UtcNow;

        // Assert
        Assert.NotNull(job.CompletedAt);
        Assert.True(job.CompletedAt >= beforeFail);
        Assert.True(job.CompletedAt <= afterFail);
    }

    [Fact]
    public void Fail_WithNullMessage_ShouldStoreNull()
    {
        // Arrange
        var job = Job.Create(Guid.NewGuid());

        // Act
        job.Fail(null!);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Null(job.ErrorMessage);
    }

    #endregion
}
