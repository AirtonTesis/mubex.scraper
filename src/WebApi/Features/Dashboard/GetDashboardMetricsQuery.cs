using Domain.ValueObjects;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.Dashboard;

public record GetDashboardMetricsQuery(DateTime StartDate, DateTime EndDate) : IRequest<DashboardMetricsDto>;

public class GetDashboardMetricsHandler : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly ApplicationDbContext _context;

    public GetDashboardMetricsHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.CreatedAt >= request.StartDate && j.CreatedAt <= request.EndDate)
            .ToListAsync(cancellationToken);

        var totalSearches = jobs.Count;
        var completedJobs = jobs.Count(j => j.Status == JobStatus.Completed);
        var failedJobs = jobs.Count(j => j.Status == JobStatus.Failed);
        var activeJobs = jobs.Count(j => j.Status == JobStatus.Active);

        var successRate = totalSearches > 0 ? (double)completedJobs / totalSearches * 100 : 0;
        var failureRate = totalSearches > 0 ? (double)failedJobs / totalSearches * 100 : 0;

        return new DashboardMetricsDto(
            totalSearches,
            Math.Round(successRate, 2),
            Math.Round(failureRate, 2),
            activeJobs,
            request.StartDate,
            request.EndDate);
    }
}
