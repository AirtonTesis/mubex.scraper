using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>
/// Entidade que representa uma entrada no histórico de execução de um Job.
/// Cada entrada registra uma transição de status com timestamp.
/// **Validates: Requirements 4.8**
/// </summary>
public class JobHistoryEntry
{
    /// <summary>
    /// Identificador único da entrada de histórico
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identificador do Job ao qual esta entrada de histórico pertence
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Status do Job no momento desta entrada
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Data e hora em que esta transição de status ocorreu (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Construtor sem parâmetros para EF Core
    /// </summary>
    public JobHistoryEntry()
    {
    }

    /// <summary>
    /// Construtor para criar uma nova entrada de histórico
    /// </summary>
    /// <param name="jobId">ID do Job</param>
    /// <param name="status">Status do Job</param>
    public JobHistoryEntry(Guid jobId, JobStatus status)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        Status = status;
        Timestamp = DateTime.UtcNow;
    }
}
