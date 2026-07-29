using Domain.ValueObjects;

namespace WebApi.Features.Jobs;

public record JobHistoryEntryDto(
    Guid Id,
    Guid JobId,
    JobStatus Status,
    DateTime Timestamp);
