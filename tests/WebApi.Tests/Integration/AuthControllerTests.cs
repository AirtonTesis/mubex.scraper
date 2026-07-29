using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApi.Tests.Integration;

/// <summary>
/// Testes de integração para AuthController.
/// Testa autenticação JWT e status codes corretos.
/// **Validates: Requirements 1.2, 1.3, 1.4**
/// </summary>
public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Testa que POST /api/auth/login retorna 200 com credenciais válidas.
    /// **Validates: Requirement 1.2**
    /// </summary>
    [Fact]
    public async Task Login_ShouldReturn200_WithValidCredentials()
    {
        // Arrange - Create user in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var password = "TestPassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = User.Create("test@example.com", passwordHash);
        Assert.True(user.IsSuccess);

        context.Users.Add(user.Value);
        await context.SaveChangesAsync();

        var request = new { Email = "test@example.com", Password = password };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Testa que POST /api/auth/login retorna 400 com credenciais inválidas.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public async Task Login_ShouldReturn400_WithInvalidCredentials()
    {
        // Arrange
        var request = new { Email = "nonexistent@example.com", Password = "WrongPassword" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Testa que POST /api/auth/login retorna 400 com senha incorreta.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public async Task Login_ShouldReturn400_WithWrongPassword()
    {
        // Arrange - Create user in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword");
        var user = User.Create("test@example.com", passwordHash);
        Assert.True(user.IsSuccess);

        context.Users.Add(user.Value);
        await context.SaveChangesAsync();

        var request = new { Email = "test@example.com", Password = "WrongPassword" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
