using Domain.Entities;
using Domain.Validation;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.Jobs;

public record ActivateJobCommand(Guid JobId) : IRequest<Result<bool>>;

public class ActivateJobHandler : IRequestHandler<ActivateJobCommand, Result<bool>>
{
    private readonly ApplicationDbContext _context;
    private readonly IQueueManager _queueManager;

    public ActivateJobHandler(ApplicationDbContext context, IQueueManager queueManager)
    {
        _context = context;
        _queueManager = queueManager;
    }

    public async Task<Result<bool>> Handle(ActivateJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken);

        if (job == null)
            return Result<bool>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("job", "id", "not_found")
            });

        try
        {
            job.Resume();
            await _context.SaveChangesAsync(cancellationToken);
            await _queueManager.UpdateJobStatusAsync(job.Id, job.Status, cancellationToken: cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(new List<ValidationKey>
            {
                ValidationKey.Custom("job", "status", ex.Message)
            });
        }
    }
}
