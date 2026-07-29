namespace Domain.Entities;

/// <summary>
/// Entidade que armazena cada item (resultado de busca) coletado durante a execução de um Job de scraping.
/// Cada linha representa uma posição encontrada nos resultados do Google para uma keyword específica.
/// </summary>
public class CollectedItem : BaseEntity
{
    /// <summary>
    /// ID do Job que coletou este item
    /// </summary>
    public Guid JobId { get; private set; }

    /// <summary>
    /// Palavra-chave que foi pesquisada
    /// </summary>
    public string Keyword { get; private set; } = string.Empty;

    /// <summary>
    /// Domínio alvo encontrado
    /// </summary>
    public string Domain { get; private set; } = string.Empty;

    /// <summary>
    /// Posição (ranking) nos resultados (1-indexed)
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// URL completa do resultado encontrado
    /// </summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>
    /// Título do resultado de busca
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Trecho/descrição do resultado
    /// </summary>
    public string Snippet { get; private set; } = string.Empty;

    /// <summary>
    /// Construtor vazio para EF Core
    /// </summary>
    private CollectedItem() { }

    /// <summary>
    /// Factory method para criar um novo CollectedItem
    /// </summary>
    public static CollectedItem Create(
        Guid jobId,
        string keyword,
        string domain,
        int position,
        string url,
        string title,
        string snippet)
    {
        return new CollectedItem
        {
            JobId = jobId,
            Keyword = keyword,
            Domain = domain,
            Position = position,
            Url = url,
            Title = title,
            Snippet = snippet
        };
    }
}
