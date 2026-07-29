using Domain.ValueObjects;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.Jobs;

public record GetAllJobsQuery : IRequest<List<JobDto>>;

public record JobDto(
    Guid Id,
    Guid SearchListId,
    string SearchListName,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int RetryCount,
    string? ErrorMessage,
    DateTime CreatedAt);

public class GetAllJobsHandler : IRequestHandler<GetAllJobsQuery, List<JobDto>>
{
    private readonly ApplicationDbContext _context;

    public GetAllJobsHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<JobDto>> Handle(GetAllJobsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Jobs
            .AsNoTracking()
            .Include(j => j.SearchList)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobDto(
                j.Id,
                j.SearchListId,
                j.SearchList != null ? j.SearchList.Name : "Desconhecida",
                j.Status.ToString(),
                j.StartedAt,
                j.CompletedAt,
                j.RetryCount,
                j.ErrorMessage,
                j.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
