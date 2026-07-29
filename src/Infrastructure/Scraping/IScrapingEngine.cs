namespace Infrastructure.Scraping;

/// <summary>
/// Interface para o motor de scraping. Implementações reais usam Playwright/Selenium.
/// </summary>
public interface IScrapingEngine
{
    /// <summary>
    /// Executa o scraping completo de um Job, processando todas as palavras-chave
    /// da SearchList associada e extraindo posições dos domínios alvo.
    /// </summary>
    Task<ScrapingResult> ExecuteAsync(Domain.Entities.Job job, CancellationToken cancellationToken = default);
}
