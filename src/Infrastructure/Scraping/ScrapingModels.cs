namespace Infrastructure.Scraping;

/// <summary>
/// Dados de uma única posição encontrada nos resultados de busca do Google.
/// </summary>
public record SearchResultData
{
    public string Keyword { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public int Position { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
}

/// <summary>
/// Resultado do processamento de um job de scraping.
/// </summary>
public record ScrapingResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SearchResultData> Data { get; init; } = new();
}
