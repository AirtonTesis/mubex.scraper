using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Features.SearchLists;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchListsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchListsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userId != null ? Guid.Parse(userId) : Guid.Empty;
    }

    /// <summary>
    /// Cria uma nova lista de busca.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateSearchListResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSearchListRequest request)
    {
        var command = new CreateSearchListCommand(
            request.Name,
            request.Keywords,
            request.Domains,
            GetCurrentUserId());

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

        return CreatedAtAction(nameof(GetAll), new { }, new { Id = result.Value });
    }

    /// <summary>
    /// Retorna todas as listas de busca do usuário autenticado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SearchListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var query = new GetAllSearchListsQuery(GetCurrentUserId());
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza uma lista de busca existente.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSearchListRequest request)
    {
        var command = new UpdateSearchListCommand(id, request.Name, request.Keywords, request.Domains);
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
    /// Remove uma lista de busca.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteSearchListCommand(id);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            var error = result.Errors.FirstOrDefault();
            if (error?.Key.Contains("not_found") == true)
                return NotFound();
        }

        return NoContent();
    }
}

public record CreateSearchListRequest(
    string Name,
    List<string> Keywords,
    List<string> Domains);

public record UpdateSearchListRequest(
    string Name,
    List<string> Keywords,
    List<string> Domains);

public record CreateSearchListResponse(Guid Id);
