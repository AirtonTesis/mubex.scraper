using Domain.Entities;
using Domain.Validation;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.Jobs;

public record EnqueueJobCommand(Guid SearchListId) : IRequest<Result<Guid>>;

public class EnqueueJobHandler : IRequestHandler<EnqueueJobCommand, Result<Guid>>
{
    private readonly ApplicationDbContext _context;
    private readonly IQueueManager _queueManager;

    public EnqueueJobHandler(ApplicationDbContext context, IQueueManager queueManager)
    {
        _context = context;
        _queueManager = queueManager;
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

        var job = Job.Create(request.SearchListId);

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        await _queueManager.EnqueueJobAsync(job, cancellationToken);

        return Result<Guid>.Success(job.Id);
    }
}
