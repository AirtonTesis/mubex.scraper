namespace WebApi.Features.Dashboard;

public record DashboardMetricsDto(
    int TotalSearches,
    double SuccessRate,
    double FailureRate,
    int ActiveExecutions,
    DateTime StartDate,
    DateTime EndDate);
