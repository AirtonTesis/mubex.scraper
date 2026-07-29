using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>
/// Entidade de domínio representando um Job de scraping com máquina de estados.
/// Gerencia o ciclo de vida de uma tarefa de extração desde criação até conclusão/falha.
/// **Validates: Requirements 4.3, 4.4, 4.5, 4.6, 4.7**
/// </summary>
public class Job : BaseEntity
{
    /// <summary>
    /// ID da SearchList associada a este Job
    /// </summary>
    public Guid SearchListId { get; private set; }

    /// <summary>
    /// Status atual do Job na máquina de estados
    /// </summary>
    public JobStatus Status { get; private set; }

    /// <summary>
    /// Data e hora em que o Job foi iniciado (UTC)
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>
    /// Data e hora em que o Job foi concluído ou falhou (UTC)
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Contador de tentativas de retry realizadas
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Mensagem de erro em caso de falha
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Quantidade de itens (resultados de busca) coletados neste Job
    /// </summary>
    public int ItemsCollected { get; private set; }

    /// <summary>
    /// Histórico de transições de status do Job
    /// </summary>
    public List<JobHistoryEntry> History { get; private set; }

    /// <summary>
    /// Propriedade de navegação para a SearchList associada
    /// </summary>
    public SearchList? SearchList { get; private set; }

    /// <summary>
    /// Construtor privado para EF Core
    /// </summary>
    private Job()
    {
        History = new List<JobHistoryEntry>();
    }

    /// <summary>
    /// Factory method para criar um novo Job.
    /// O Job inicia no estado Pending e registra a primeira entrada no histórico.
    /// </summary>
    /// <param name="searchListId">ID da SearchList a ser processada</param>
    /// <returns>Nova instância de Job</returns>
    public static Job Create(Guid searchListId)
    {
        var job = new Job
        {
            SearchListId = searchListId,
            Status = JobStatus.Pending,
            RetryCount = 0
        };

        job.AddHistoryEntry(JobStatus.Pending);
        return job;
    }

    /// <summary>
    /// Inicia o processamento do Job.
    /// Transição válida: Pending → Active ou Paused → Active
    /// **Validates: Requirement 4.4**
    /// </summary>
    /// <exception cref="InvalidOperationException">Se o Job não estiver em estado válido para iniciar</exception>
    public void Start()
    {
        if (Status != JobStatus.Pending && Status != JobStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Cannot start job in {Status} status. Valid states: Pending, Paused");
        }

        Status = JobStatus.Active;
        StartedAt ??= DateTime.UtcNow; // Define StartedAt apenas na primeira vez
        AddHistoryEntry(JobStatus.Active);
        Touch();
    }

    /// <summary>
    /// Pausa o processamento do Job.
    /// Transição válida: Active → Paused
    /// **Validates: Requirement 4.5**
    /// </summary>
    /// <exception cref="InvalidOperationException">Se o Job não estiver ativo</exception>
    public void Pause()
    {
        if (Status != JobStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot pause job in {Status} status. Must be Active");
        }

        Status = JobStatus.Paused;
        AddHistoryEntry(JobStatus.Paused);
        Touch();
    }

    /// <summary>
    /// Retoma o processamento de um Job pausado.
    /// Transição válida: Paused → Active
    /// **Validates: Requirement 4.6**
    /// </summary>
    /// <exception cref="InvalidOperationException">Se o Job não estiver pausado</exception>
    public void Resume()
    {
        if (Status != JobStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Cannot resume job in {Status} status. Must be Paused");
        }

        Status = JobStatus.Active;
        AddHistoryEntry(JobStatus.Active);
        Touch();
    }

    /// <summary>
    /// Marca o Job como concluído com sucesso.
    /// Transição válida: Active → Completed
    /// **Validates: Requirement 4.7**
    /// </summary>
    /// <param name="itemsCollected">Quantidade de itens coletados</param>
    /// <exception cref="InvalidOperationException">Se o Job não estiver ativo</exception>
    public void Complete(int itemsCollected = 0)
    {
        if (Status != JobStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot complete job in {Status} status. Must be Active");
        }

        Status = JobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ItemsCollected = itemsCollected;
        AddHistoryEntry(JobStatus.Completed);
        Touch();
    }

    /// <summary>
    /// Marca o Job como falho após todas as tentativas de retry.
    /// Pode ser chamado de qualquer estado exceto Completed.
    /// **Validates: Requirement 4.7**
    /// </summary>
    /// <param name="errorMessage">Mensagem descritiva do erro ocorrido</param>
    public void Fail(string errorMessage)
    {
        if (Status == JobStatus.Completed)
        {
            throw new InvalidOperationException(
                "Cannot fail a job that is already completed");
        }

        Status = JobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        AddHistoryEntry(JobStatus.Failed);
        Touch();
    }

    /// <summary>
    /// Incrementa o contador de tentativas de retry.
    /// Usado para rastrear quantas vezes o Job foi retentado após falhas.
    /// </summary>
    public void IncrementRetry()
    {
        RetryCount++;
        Touch();
    }

    /// <summary>
    /// Adiciona uma nova entrada ao histórico de transições de status.
    /// Registra o status atual com timestamp UTC.
    /// **Validates: Requirement 4.8**
    /// </summary>
    /// <param name="status">Status a ser registrado no histórico</param>
    private void AddHistoryEntry(JobStatus status)
    {
        History.Add(new JobHistoryEntry(Id, status));
    }
}
