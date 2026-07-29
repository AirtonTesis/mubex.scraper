using Domain.Entities;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using WebApi.Features.Auth;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.Auth;

/// <summary>
/// Testes de integração para LoginCommand.
/// **Property 2: Invalid Credentials Return Validation Keys**
/// **Valida: Requirements 1.3**
/// </summary>
public class LoginCommandTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandTests()
    {
        _context = TestDbContextFactory.Create();

        // Setup JWT service for testing
        var inMemorySettings = new Dictionary<string, string>
        {
            { "Jwt:Secret", "test-secret-key-min-32-characters-long-for-hmacsha256" },
            { "Jwt:Issuer", "test-issuer" },
            { "Jwt:Audience", "test-audience" },
            { "Jwt:ExpirationMinutes", "60" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtTokenService = new JwtTokenService(configuration);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que Login retorna token JWT com credenciais válidas.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnToken_WithValidCredentials()
    {
        // Arrange - Create a user with BCrypt hashed password
        var password = "TestPassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var userResult = User.Create("test@example.com", passwordHash);
        Assert.True(userResult.IsSuccess);

        _context.Users.Add(userResult.Value);
        await _context.SaveChangesAsync();

        var command = new LoginCommand("test@example.com", password);
        var handler = new LoginHandler(_context, _jwtTokenService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Token);
        Assert.NotEmpty(result.Value.Token);
        Assert.Equal(userResult.Value.Id, result.Value.UserId);
        Assert.Equal("test@example.com", result.Value.Email);
    }

    /// <summary>
    /// Testa que Login retorna falha com chave de validação quando email não existe.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WithInvalidEmail()
    {
        // Arrange
        var command = new LoginCommand("nonexistent@example.com", "password");
        var handler = new LoginHandler(_context, _jwtTokenService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key.Contains("credentials"));
    }

    /// <summary>
    /// Testa que Login retorna falha com chave de validação quando senha está incorreta.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WithInvalidPassword()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword");
        var userResult = User.Create("test@example.com", passwordHash);
        Assert.True(userResult.IsSuccess);

        _context.Users.Add(userResult.Value);
        await _context.SaveChangesAsync();

        var command = new LoginCommand("test@example.com", "WrongPassword");
        var handler = new LoginHandler(_context, _jwtTokenService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key.Contains("credentials"));
    }

    /// <summary>
    /// Testa que Login é case-insensitive para email.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldBeCaseInsensitive_ForEmail()
    {
        // Arrange
        var password = "TestPassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var userResult = User.Create("test@example.com", passwordHash);
        Assert.True(userResult.IsSuccess);

        _context.Users.Add(userResult.Value);
        await _context.SaveChangesAsync();

        // Login with different case
        var command = new LoginCommand("TEST@EXAMPLE.COM", password);
        var handler = new LoginHandler(_context, _jwtTokenService);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
