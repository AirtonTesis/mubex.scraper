namespace Domain.ValueObjects;

/// <summary>
/// Enumeração que representa os possíveis estados de um Job de scraping.
/// **Validates: Requirements 4.3**
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job foi criado e está aguardando processamento
    /// </summary>
    Pending,

    /// <summary>
    /// Job está sendo processado ativamente
    /// </summary>
    Active,

    /// <summary>
    /// Job foi pausado manualmente pelo usuário
    /// </summary>
    Paused,

    /// <summary>
    /// Job foi concluído com sucesso
    /// </summary>
    Completed,

    /// <summary>
    /// Job falhou após todas as tentativas de retry
    /// </summary>
    Failed
}
