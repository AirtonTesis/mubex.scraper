using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Features.Dashboard;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retorna métricas do dashboard para um período específico.
    /// </summary>
    /// <param name="startDate">Data de início do período</param>
    /// <param name="endDate">Data de fim do período</param>
    /// <returns>Métricas calculadas: TotalSearches, SuccessRate, FailureRate, ActiveExecutions</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(DashboardMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var query = new GetDashboardMetricsQuery(start, end);
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
