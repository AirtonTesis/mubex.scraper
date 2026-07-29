using Domain.Entities;
using Domain.ValueObjects;

namespace Infrastructure.Queue;

/// <summary>
/// Interface para gerenciamento de fila de Jobs de scraping.
/// Define operações de enqueue, dequeue e atualização de status.
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public interface IQueueManager
{
    /// <summary>
    /// Adiciona um Job à fila para processamento assíncrono.
    /// **Validates: Requirement 4.1**
    /// </summary>
    Task EnqueueJobAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove e retorna o próximo Job da fila para processamento.
    /// Retorna null se a fila estiver vazia.
    /// **Validates: Requirement 4.2**
    /// </summary>
    Task<Job?> DequeueJobAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status de um Job na fila.
    /// **Validates: Requirement 4.3**
    /// </summary>
    Task UpdateJobStatusAsync(Guid jobId, JobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
}
