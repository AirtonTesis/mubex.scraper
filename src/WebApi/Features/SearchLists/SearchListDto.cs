namespace WebApi.Features.SearchLists;

public record SearchListDto(
    Guid Id,
    string Name,
    List<string> Keywords,
    List<string> Domains,
    Guid UserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Campos de status do último job
    Guid? LatestJobId,
    string? LatestJobStatus,
    DateTime? LatestJobCreatedAt,
    int TotalJobs,
    int CompletedJobs,
    int FailedJobs,
    // Quantidade total de itens coletados em todos os jobs
    int TotalItemsCollected);
