using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Domain.Entities;
using Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Infrastructure.Tests.Authentication;

/// <summary>
/// Testes unitários para JwtTokenService.
/// Valida geração de tokens, validação de assinatura e expiração.
/// **Validates: Requirements 1.1, 1.2, 1.5**
/// </summary>
public class JwtTokenServiceTests
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        // Configurar settings de teste para JWT
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Secret", "este-e-um-segredo-muito-longo-para-hmacsha256-com-mais-de-32-caracteres"},
            {"Jwt:Issuer", "test-issuer"},
            {"Jwt:Audience", "test-audience"},
            {"Jwt:ExpirationMinutes", "60"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtTokenService = new JwtTokenService(_configuration);
    }

    /// <summary>
    /// Testa a geração de token JWT com claims corretos.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldReturnValidToken_WithCorrectClaims()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        Assert.True(userResult.IsSuccess);
        var user = userResult.Value;

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Decodificar token para verificar claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Verificar claim NameIdentifier
        var nameIdentifierClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        Assert.NotNull(nameIdentifierClaim);
        Assert.Equal(user.Id.ToString(), nameIdentifierClaim.Value);

        // Verificar claim Email
        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal(user.Email, emailClaim.Value);

        // Verificar claim Jti (token ID único)
        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jtiClaim);
        Assert.True(Guid.TryParse(jtiClaim.Value, out _));
    }

    /// <summary>
    /// Testa que o algoritmo de assinatura é HmacSha256.
    /// **Validates: Requirement 1.2**
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldUseHmacSha256Algorithm()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        Assert.Equal("HS256", jwtToken.Header.Alg);
    }

    /// <summary>
    /// Testa que o token é gerado com o issuer e audience corretos.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeCorrectIssuerAndAudience()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        Assert.Equal("test-issuer", jwtToken.Issuer);
        Assert.Contains("test-audience", jwtToken.Audiences);
    }

    /// <summary>
    /// Testa que o token gerado tem data de expiração configurada.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldSetExpirationTime()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        Assert.True(jwtToken.ValidTo > beforeGeneration);
        Assert.True(jwtToken.ValidTo <= beforeGeneration.AddMinutes(60).AddSeconds(5)); // 5 segundos de tolerância
    }

    /// <summary>
    /// Testa que cada token gerado tem um Jti único.
    /// **Validates: Requirement 1.2**
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldGenerateUniqueJti()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;

        // Act
        var token1 = _jwtTokenService.GenerateToken(user);
        var token2 = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken1 = handler.ReadJwtToken(token1);
        var jwtToken2 = handler.ReadJwtToken(token2);

        var jti1 = jwtToken1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwtToken2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        Assert.NotEqual(jti1, jti2);
    }

    /// <summary>
    /// Testa que ValidateToken retorna ClaimsPrincipal correto para token válido.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    [Fact]
    public void ValidateToken_ShouldReturnClaimsPrincipal_ForValidToken()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;
        var token = _jwtTokenService.GenerateToken(user);

        // Act
        var principal = _jwtTokenService.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        Assert.NotNull(principal.Identity);
        Assert.True(principal.Identity.IsAuthenticated);

        var nameIdentifier = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(user.Id.ToString(), nameIdentifier);

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        Assert.Equal(user.Email, email);
    }

    /// <summary>
    /// Testa que ValidateToken rejeita token com assinatura inválida.
    /// **Validates: Requirement 1.5**
    /// </summary>
    [Fact]
    public void ValidateToken_ShouldReturnNull_ForTokenWithInvalidSignature()
    {
        // Arrange
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;
        var token = _jwtTokenService.GenerateToken(user);
        
        // Corromper a assinatura do token
        var parts = token.Split('.');
        var corruptedToken = $"{parts[0]}.{parts[1]}.invalid-signature";

        // Act
        var principal = _jwtTokenService.ValidateToken(corruptedToken);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Testa que ValidateToken rejeita token expirado.
    /// **Validates: Requirement 1.5**
    /// </summary>
    [Fact]
    public void ValidateToken_ShouldReturnNull_ForExpiredToken()
    {
        // Arrange - Criar configuração com expiração de -1 minuto (já expirado)
        var expiredSettings = new Dictionary<string, string>
        {
            {"Jwt:Secret", "este-e-um-segredo-muito-longo-para-hmacsha256-com-mais-de-32-caracteres"},
            {"Jwt:Issuer", "test-issuer"},
            {"Jwt:Audience", "test-audience"},
            {"Jwt:ExpirationMinutes", "-1"}
        };

        var expiredConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(expiredSettings!)
            .Build();

        var expiredJwtService = new JwtTokenService(expiredConfig);
        
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;
        var expiredToken = expiredJwtService.GenerateToken(user);

        // Act
        var principal = _jwtTokenService.ValidateToken(expiredToken);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Testa que ValidateToken rejeita token com issuer incorreto.
    /// **Validates: Requirement 1.5**
    /// </summary>
    [Fact]
    public void ValidateToken_ShouldReturnNull_ForTokenWithInvalidIssuer()
    {
        // Arrange - Criar serviço com issuer diferente
        var differentSettings = new Dictionary<string, string>
        {
            {"Jwt:Secret", "este-e-um-segredo-muito-longo-para-hmacsha256-com-mais-de-32-caracteres"},
            {"Jwt:Issuer", "different-issuer"},
            {"Jwt:Audience", "test-audience"},
            {"Jwt:ExpirationMinutes", "60"}
        };

        var differentConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(differentSettings!)
            .Build();

        var differentJwtService = new JwtTokenService(differentConfig);
        
        var userResult = User.Create("test@example.com", "hashed-password-value-with-minimum-60-characters-long-string-bcrypt");
        var user = userResult.Value;
        var token = differentJwtService.GenerateToken(user);

        // Act - Validar com serviço original (issuer diferente)
        var principal = _jwtTokenService.ValidateToken(token);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Testa que ValidateToken rejeita token vazio ou nulo.
    /// **Validates: Requirement 1.5**
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateToken_ShouldReturnNull_ForNullOrEmptyToken(string? token)
    {
        // Act
        var principal = _jwtTokenService.ValidateToken(token!);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Testa que ValidateToken rejeita token malformado.
    /// **Validates: Requirement 1.5**
    /// </summary>
    [Fact]
    public void ValidateToken_ShouldReturnNull_ForMalformedToken()
    {
        // Arrange
        var malformedToken = "this.is.not.a.valid.jwt.token";

        // Act
        var principal = _jwtTokenService.ValidateToken(malformedToken);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>
    /// Testa que GenerateToken lança exceção para usuário nulo.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldThrowArgumentNullException_ForNullUser()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _jwtTokenService.GenerateToken(null!));
    }

    /// <summary>
    /// Testa que o construtor lança exceção se JWT Secret não estiver configurado.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSecretIsNotConfigured()
    {
        // Arrange
        var invalidSettings = new Dictionary<string, string>
        {
            {"Jwt:Issuer", "test-issuer"},
            {"Jwt:Audience", "test-audience"},
            {"Jwt:ExpirationMinutes", "60"}
            // JWT:Secret ausente
        };

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(invalidSettings!)
            .Build();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JwtTokenService(invalidConfig));
    }
}
