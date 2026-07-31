using Domain.Entities;
using Domain.Validation;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WebApi.Features.Jobs;

public record EnqueueJobCommand(Guid SearchListId) : IRequest<Result<Guid>>;

public class EnqueueJobHandler : IRequestHandler<EnqueueJobCommand, Result<Guid>>
{
    private readonly ApplicationDbContext _context;
    private readonly IQueueManager _queueManager;
    private readonly ILogger<EnqueueJobHandler> _logger;

    public EnqueueJobHandler(ApplicationDbContext context, IQueueManager queueManager, ILogger<EnqueueJobHandler> logger)
    {
        _context = context;
        _queueManager = queueManager;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(EnqueueJobCommand request, CancellationToken cancellationToken)
    {
        // Verify SearchList exists
        var searchListExists = await _context.SearchLists
            .AnyAsync(s => s.Id == request.SearchListId, cancellationToken);

        if (!searchListExists)
        {
            return Result<Guid>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("search_list", "id", "not_found")
            });
        }

        // Usar SQL direto para evitar DbUpdateConcurrencyException do EF Core
        var now = DateTime.UtcNow;
        var newJobId = Guid.NewGuid();

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        // 1. Cancelar jobs Active/Pending existentes (via SQL, sem ChangeTracker)
        var cancelledCount = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Jobs"" 
              SET ""Status"" = {0}, ""UpdatedAt"" = {1}, ""CompletedAt"" = {1}, ""ErrorMessage"" = {2}
              WHERE ""SearchListId"" = {3} AND (""Status"" = {4} OR ""Status"" = {5})",
            (int)JobStatus.Failed, now,
            "Cancelado — novo job enfileirado para esta lista",
            request.SearchListId,
            (int)JobStatus.Active, (int)JobStatus.Pending);

        if (cancelledCount > 0)
        {
            _logger.LogInformation("{Count} job(s) cancelados para SearchList {ListId}", cancelledCount, request.SearchListId);
        }

        // 2. Inserir novo job via SQL
        await _context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Jobs"" 
                (""Id"", ""SearchListId"", ""Status"", ""RetryCount"", ""ItemsCollected"", ""CreatedAt"", ""UpdatedAt"")
              VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            newJobId, request.SearchListId,
            (int)JobStatus.Pending, 0, 0, now, now);

        // 3. Inserir entrada de historico para o novo job
        await _context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""JobHistoryEntries"" (""Id"", ""JobId"", ""Status"", ""Timestamp"")
              VALUES ({0}, {1}, {2}, {3})",
            Guid.NewGuid(), newJobId, (int)JobStatus.Pending, now);

        await tx.CommitAsync(cancellationToken);

        // 4. Criar objeto Job temporario para enfileirar (o queue manager so precisa do Id)
        var tempJob = Job.Create(Guid.Empty);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(tempJob, newJobId);

        await _queueManager.EnqueueJobAsync(tempJob, cancellationToken);

        _logger.LogInformation("Job {JobId} criado via SQL direto para SearchList {ListId}", newJobId, request.SearchListId);
        return Result<Guid>.Success(newJobId);
    }
}
