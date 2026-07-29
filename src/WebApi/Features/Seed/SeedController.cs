using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserEntity = Domain.Entities.User;

namespace WebApi.Features.Seed;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SeedController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Cria um usuário de teste para desenvolvimento.
    /// </summary>
    [HttpPost("user")]
    public async Task<IActionResult> SeedUser()
    {
        var email = "test@example.com";
        var password = "TestPassword123!";

        // Verificar se o usuário já existe
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existingUser != null)
        {
            return Ok(new
            {
                message = "Usuário já existe",
                email = email,
                password = password
            });
        }

        // Criar hash da senha
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        // Criar usuário
        var userResult = UserEntity.Create(email, passwordHash);
        if (!userResult.IsSuccess)
        {
            return BadRequest(new { errors = userResult.Errors.Select(e => e.Key) });
        }

        _context.Users.Add(userResult.Value);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Usuário criado com sucesso",
            email = email,
            password = password,
            userId = userResult.Value.Id
        });
    }
}
