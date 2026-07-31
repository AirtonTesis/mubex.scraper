using Domain.Entities;
using Domain.Validation;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WebApi.Features.Jobs;

public record PauseJobCommand(Guid JobId) : IRequest<Result<bool>>;

public class PauseJobHandler : IRequestHandler<PauseJobCommand, Result<bool>>
{
    private readonly ApplicationDbContext _context;
    private readonly IQueueManager _queueManager;
    private readonly ILogger<PauseJobHandler> _logger;

    public PauseJobHandler(ApplicationDbContext context, IQueueManager queueManager, ILogger<PauseJobHandler> logger)
    {
        _context = context;
        _queueManager = queueManager;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(PauseJobCommand request, CancellationToken cancellationToken)
    {
        // Usar SQL direto para evitar DbUpdateConcurrencyException do EF Core
        // (o ChangeTracker do EF Core conflita com o BackgroundWorker que modifica o mesmo job)
        return await PauseWithDirectSqlAsync(request.JobId, cancellationToken);
    }

    private async Task<Result<bool>> PauseWithDirectSqlAsync(Guid jobId, CancellationToken cancellationToken)
    {
        // UPDATE direto na tabela Jobs — só afeta se o job ainda estiver Active
        var affected = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Jobs"" 
              SET ""Status"" = {0}, ""UpdatedAt"" = {1}
              WHERE ""Id"" = {2} AND ""Status"" = {3}",
            (int)JobStatus.Paused,
            DateTime.UtcNow,
            jobId,
            (int)JobStatus.Active);

        if (affected <= 0)
        {
            _logger.LogInformation("Job {JobId} nao estava Active para pausar (ja foi processado).", jobId);
            return Result<bool>.Success(true);
        }

        // Inserir entrada no historico
        await _context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""JobHistoryEntries"" (""Id"", ""JobId"", ""Status"", ""Timestamp"")
              VALUES ({0}, {1}, {2}, {3})",
            Guid.NewGuid(),
            jobId,
            (int)JobStatus.Paused,
            DateTime.UtcNow);

        await _queueManager.UpdateJobStatusAsync(jobId, JobStatus.Paused, cancellationToken: cancellationToken);

        _logger.LogInformation("Job {JobId} pausado com sucesso via SQL direto.", jobId);
        return Result<bool>.Success(true);
    }
}
