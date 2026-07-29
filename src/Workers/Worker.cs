using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

namespace Workers;

/// <summary>
/// Worker de scraping que consome jobs da fila e executa extração real de dados
/// utilizando Playwright para navegação furtiva no Google Search.
/// Processa jobs em 3 fases isoladas para evitar conflitos de change tracking no EF Core.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IScrapingEngine _scrapingEngine;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IScrapingEngine scrapingEngine)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _scrapingEngine = scrapingEngine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de scraping iniciado com motor Playwright");

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
                _logger.LogError(ex, "Erro inesperado no loop do worker");
            }

            await Task.Delay(2000, stoppingToken);
        }

        _logger.LogInformation("Worker de scraping encerrado");
    }

    private async Task ProcessNextJobAsync(CancellationToken cancellationToken)
    {
        // Dequeue retorna apenas o Id do job
        var jobId = await DequeueNextJobIdAsync(cancellationToken);
        if (jobId == null)
            return;

        _logger.LogInformation("Job {JobId} retirado da fila, iniciando processamento", jobId);

        // FASE 1: Transição Pending → Active (scope separado)
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await dbContext.Jobs
                .Include(j => j.SearchList)
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("Job {JobId} não encontrado no banco de dados", jobId);
                return;
            }

            _logger.LogInformation(
                "Processando job {JobId} - Lista: '{ListName}' ({KeywordCount} keywords)",
                job.Id,
                job.SearchList?.Name ?? "desconhecida",
                job.SearchList?.Keywords.Count ?? 0);

            job.Start();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // FASE 2: Executar scraping real com Playwright (scope separado)
        ScrapingResult scrapingResult = new ScrapingResult { IsSuccess = false, ErrorMessage = "Scraping não executado" };
        try
        {
            // Recarregar job + searchList em scope separado para o engine
            Job jobForScraping;
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                jobForScraping = await dbContext.Jobs
                    .Include(j => j.SearchList)
                    .FirstAsync(j => j.Id == jobId, cancellationToken);
            }

            scrapingResult = await _scrapingEngine.ExecuteAsync(jobForScraping, cancellationToken);

            if (scrapingResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Scraping concluído para job {JobId}: {ResultCount} posições encontradas",
                    jobId, scrapingResult.Data.Count);
            }
            else
            {
                _logger.LogWarning(
                    "Scraping parcial para job {JobId}: {Error}",
                    jobId, scrapingResult.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante execução do scraping para job {JobId}", jobId);
            scrapingResult = new ScrapingResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }

        // FASE 3: Transição Active → Completed/Failed (scope separado)
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
            if (scrapingResult.IsSuccess)
            {
                job.Complete(scrapingResult.Data.Count);
                _logger.LogInformation("Job {JobId} concluído com sucesso - {Items} itens", job.Id, scrapingResult.Data.Count);
            }
            else
            {
                var errorMsg = scrapingResult.ErrorMessage ?? "Erro desconhecido no scraping";
                job.Fail(errorMsg);
                _logger.LogWarning("Job {JobId} marcado como falho: {Error}", job.Id, errorMsg);
            }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar status final do job {JobId}", job.Id);

                try
                {
                    if (job.Status != JobStatus.Failed)
                    {
                        job.Fail($"Erro interno: {ex.Message}");
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
