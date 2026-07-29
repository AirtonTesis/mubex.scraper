namespace Domain.Entities;

/// <summary>
/// Classe base abstrata para todas as entidades do domínio.
/// Fornece propriedades comuns de rastreamento e identificação.
/// **Validates: Requirements 8.2, 12.1, 12.2**
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Identificador único da entidade
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Data e hora de criação da entidade em UTC
    /// </summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>
    /// Data e hora da última atualização da entidade em UTC
    /// </summary>
    public DateTime UpdatedAt { get; protected set; }

    /// <summary>
    /// Construtor protegido para inicialização de entidades.
    /// Gera novo Id e define timestamps para o momento atual em UTC.
    /// </summary>
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza o timestamp UpdatedAt para o momento atual em UTC.
    /// Deve ser chamado sempre que a entidade for modificada.
    /// </summary>
    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
