using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Worker de scraping embutido na WebApi que executa scraping REAL com Playwright.
/// Consome jobs da fila in-memory, navega no Google Search, extrai resultados
/// e armazena cada item coletado na tabela CollectedItems.
/// </summary>
public class ScrapingBackgroundWorker : BackgroundService
{
    private readonly ILogger<ScrapingBackgroundWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ScrapingBackgroundWorker(ILogger<ScrapingBackgroundWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScrapingBackgroundWorker iniciado com motor Playwright REAL");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ScrapingBackgroundWorker");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken cancellationToken)
    {
        var placeholder = await DequeueNextJobIdAsync(cancellationToken);
        if (placeholder == null)
            return;

        var jobId = placeholder.Value;

        // FASE 1: Transição Pending → Active
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await dbContext.Jobs
                .Include(j => j.SearchList)
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("Job {JobId} não encontrado no banco", jobId);
                return;
            }

            _logger.LogInformation("Job {JobId} retirado da fila - Lista: '{ListName}' ({KeywordCount} keywords)",
                job.Id, job.SearchList?.Name ?? "desconhecida", job.SearchList?.Keywords.Count ?? 0);

            job.Start();
            foreach (var entry in dbContext.ChangeTracker.Entries<JobHistoryEntry>())
            {
                if (entry.State == EntityState.Modified)
                    entry.State = EntityState.Added;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // FASE 2: Scraping REAL com Playwright
        ScrapingResult scrapingResult = new ScrapingResult { IsSuccess = false, ErrorMessage = "Scraping não executado" };
        try
        {
            Job jobForScraping;
            IScrapingEngine scrapingEngine;
            using (var loadScope = _serviceProvider.CreateScope())
            {
                var dbContext = loadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                jobForScraping = await dbContext.Jobs
                    .Include(j => j.SearchList)
                    .FirstAsync(j => j.Id == jobId, cancellationToken);
                scrapingEngine = loadScope.ServiceProvider.GetRequiredService<IScrapingEngine>();
            }

            _logger.LogInformation("Iniciando scraping REAL para job {JobId}...", jobId);
            scrapingResult = await scrapingEngine.ExecuteAsync(jobForScraping, cancellationToken);

            _logger.LogInformation("Scraping para job {JobId}: {Success} - {Count} itens encontrados",
                jobId, scrapingResult.IsSuccess ? "sucesso" : "falha", scrapingResult.Data.Count);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante execução do scraping para job {JobId}", jobId);
            scrapingResult = new ScrapingResult { IsSuccess = false, ErrorMessage = ex.Message };
        }

        // FASE 3: Salvar resultados no banco e marcar job como Completed/Failed
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await dbContext.Jobs
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("Job {JobId} não encontrado para finalizar", jobId);
                return;
            }

            try
            {
                if (scrapingResult.IsSuccess && scrapingResult.Data.Count > 0)
                {
                    // Salvar cada item coletado no banco
                    foreach (var item in scrapingResult.Data)
                    {
                        var collectedItem = CollectedItem.Create(
                            jobId: job.Id,
                            keyword: item.Keyword,
                            domain: item.Domain,
                            position: item.Position,
                            url: item.Url,
                            title: item.Title,
                            snippet: item.Snippet);
                        dbContext.CollectedItems.Add(collectedItem);
                    }

                    job.Complete(scrapingResult.Data.Count);
                    _logger.LogInformation(
                        "Job {JobId} concluído com sucesso - {Items} itens REAIS coletados e salvos no banco",
                        job.Id, scrapingResult.Data.Count);
                }
                else if (scrapingResult.IsSuccess)
                {
                    // Scraping OK mas nenhum resultado dos domínios alvo
                    job.Complete(0);
                    _logger.LogWarning(
                        "Job {JobId} concluído mas nenhum resultado dos domínios alvo foi encontrado",
                        job.Id);
                }
                else
                {
                    var errorMsg = scrapingResult.ErrorMessage ?? "Erro desconhecido no scraping";
                    job.Fail(errorMsg);
                    _logger.LogWarning("Job {JobId} marcado como falho: {Error}", job.Id, errorMsg);
                }

                foreach (var entry in dbContext.ChangeTracker.Entries<JobHistoryEntry>())
                {
                    if (entry.State == EntityState.Modified)
                        entry.State = EntityState.Added;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar resultados do job {JobId}", job.Id);
                try
                {
                    if (job.Status != JobStatus.Failed)
                    {
                        job.Fail($"Erro ao salvar resultados: {ex.Message}");
                        foreach (var entry in dbContext.ChangeTracker.Entries<JobHistoryEntry>())
                        {
                            if (entry.State == EntityState.Modified)
                                entry.State = EntityState.Added;
                        }
                        await dbContext.SaveChangesAsync(CancellationToken.None);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Falha crítica ao registrar erro do job {JobId}", job.Id);
                }
            }
        }
    }

    private async Task<Guid?> DequeueNextJobIdAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();
        var placeholder = await queueManager.DequeueJobAsync(cancellationToken);
        return placeholder?.Id;
    }
}
