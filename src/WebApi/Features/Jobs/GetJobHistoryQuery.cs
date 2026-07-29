using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.Jobs;

public record GetJobHistoryQuery(Guid JobId) : IRequest<List<JobHistoryEntryDto>>;

public class GetJobHistoryHandler : IRequestHandler<GetJobHistoryQuery, List<JobHistoryEntryDto>>
{
    private readonly ApplicationDbContext _context;

    public GetJobHistoryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<JobHistoryEntryDto>> Handle(GetJobHistoryQuery request, CancellationToken cancellationToken)
    {
        return await _context.JobHistoryEntries
            .AsNoTracking()
            .Where(h => h.JobId == request.JobId)
            .OrderBy(h => h.Timestamp)
            .Select(h => new JobHistoryEntryDto(
                h.Id,
                h.JobId,
                h.Status,
                h.Timestamp))
            .ToListAsync(cancellationToken);
    }
}
