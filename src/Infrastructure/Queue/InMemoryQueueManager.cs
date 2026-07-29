using System.Threading.Channels;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Queue;

/// <summary>
/// Implementação in-memory do IQueueManager usando System.Threading.Channels.
/// Ideal para desenvolvimento local sem dependência de RabbitMQ.
/// </summary>
public class InMemoryQueueManager : IQueueManager
{
    // Canal estático compartilhado entre todas as instâncias (singleton garante isso)
    private static readonly Channel<Guid> _jobChannel = Channel.CreateUnbounded<Guid>();
    private readonly ILogger<InMemoryQueueManager> _logger;

    public InMemoryQueueManager(ILogger<InMemoryQueueManager> logger)
    {
        _logger = logger;
    }

    public async Task EnqueueJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        await _jobChannel.Writer.WriteAsync(job.Id, cancellationToken);
        _logger.LogInformation("Job {JobId} enfileirado para processamento", job.Id);
    }

    public Task<Job?> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        if (!_jobChannel.Reader.TryRead(out var jobId))
            return Task.FromResult<Job?>(null);

        _logger.LogInformation("Job {JobId} retirado da fila", jobId);

        // Criar um Job temporário apenas para transportar o Id ao worker
        // O worker buscará o Job completo no banco usando esse Id
        var tempJob = Job.Create(Guid.Empty);
        typeof(BaseEntity)
            .GetProperty("Id")!
            .SetValue(tempJob, jobId);

        return Task.FromResult<Job?>(tempJob);
    }

    public Task UpdateJobStatusAsync(Guid jobId, JobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Job {JobId} -> {Status}", jobId, status);
        return Task.CompletedTask;
    }
}
