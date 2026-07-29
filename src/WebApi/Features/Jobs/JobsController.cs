using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Features.Jobs;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly IMediator _mediator;

    public JobsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista todos os jobs.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<JobDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var query = new GetAllJobsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Enfileira um novo job de scraping.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EnqueueJobResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enqueue([FromBody] EnqueueJobRequest request)
    {
        var command = new EnqueueJobCommand(request.SearchListId);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Errors = result.Errors.ToDictionary(e => e.Key, e => new[] { e.Key })
            });
        }

        return CreatedAtAction(nameof(GetHistory), new { id = result.Value }, new { Id = result.Value });
    }

    /// <summary>
    /// Pausa um job em execução.
    /// </summary>
    [HttpPatch("{id}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pause(Guid id)
    {
        var command = new PauseJobCommand(id);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            var error = result.Errors.FirstOrDefault();
            if (error?.Key.Contains("not_found") == true)
                return NotFound();

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Errors = result.Errors.ToDictionary(e => e.Key, e => new[] { e.Key })
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Retoma um job pausado.
    /// </summary>
    [HttpPatch("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var command = new ActivateJobCommand(id);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            var error = result.Errors.FirstOrDefault();
            if (error?.Key.Contains("not_found") == true)
                return NotFound();

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Errors = result.Errors.ToDictionary(e => e.Key, e => new[] { e.Key })
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Retorna o histórico de execução de um job.
    /// </summary>
    [HttpGet("{id}/history")]
    [ProducesResponseType(typeof(List<JobHistoryEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var query = new GetJobHistoryQuery(id);
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}

public record EnqueueJobRequest(Guid SearchListId);
public record EnqueueJobResponse(Guid Id);
