using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Features.SearchLists;

public record GetAllSearchListsQuery(Guid UserId) : IRequest<List<SearchListDto>>;

public class GetAllSearchListsHandler : IRequestHandler<GetAllSearchListsQuery, List<SearchListDto>>
{
    private readonly ApplicationDbContext _context;

    public GetAllSearchListsHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SearchListDto>> Handle(GetAllSearchListsQuery request, CancellationToken cancellationToken)
    {
        // 1. Buscar todas as listas do usuário
        var lists = await _context.SearchLists
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        if (lists.Count == 0)
            return new List<SearchListDto>();

        var listIds = lists.Select(l => l.Id).ToList();

        // 2. Buscar todos os jobs das listas do usuário (uma única query simples)
        var allJobs = await _context.Jobs
            .AsNoTracking()
            .Where(j => listIds.Contains(j.SearchListId))
            .Select(j => new
            {
                j.SearchListId,
                j.Id,
                Status = j.Status.ToString(),
                j.CreatedAt,
                j.ItemsCollected
            })
            .ToListAsync(cancellationToken);

        // 3. Calcular stats em memória (evita problemas de GroupBy no EF Core + SQLite)
        var jobsByList = allJobs.GroupBy(j => j.SearchListId).ToDictionary(g => g.Key);

        return lists.Select(s =>
        {
            var jobs = jobsByList.GetValueOrDefault(s.Id);
            var jobsList = jobs?.ToList();

            var total = jobsList?.Count ?? 0;
            var completed = jobsList?.Count(j => j.Status == "Completed") ?? 0;
            var failed = jobsList?.Count(j => j.Status == "Failed") ?? 0;
            var latestJob = jobsList?.OrderByDescending(j => j.CreatedAt).FirstOrDefault();

            var totalItems = jobsList?.Sum(j => j.ItemsCollected) ?? 0;

            return new SearchListDto(
                s.Id,
                s.Name,
                s.Keywords,
                s.Domains,
                s.UserId,
                s.CreatedAt,
                s.UpdatedAt,
                latestJob?.Id,
                latestJob?.Status,
                latestJob?.CreatedAt,
                total,
                completed,
                failed,
                totalItems);
        }).ToList();
    }
}
