using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Features.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Autentica um usuário e retorna um token JWT.
    /// </summary>
    /// <param name="request">Credenciais de login</param>
    /// <returns>Token JWT ou erro de validação</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password);
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

        return Ok(result.Value);
    }
}

public record LoginRequest(string Email, string Password);
