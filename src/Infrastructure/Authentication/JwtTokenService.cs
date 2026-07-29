using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Authentication;

/// <summary>
/// Implementação do serviço de tokens JWT para autenticação de usuários.
/// Utiliza algoritmo HmacSha256 para assinatura de tokens.
/// **Validates: Requirements 1.1, 1.2, 1.5**
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _key;

    /// <summary>
    /// Inicializa o serviço JWT com configurações da aplicação.
    /// Carrega a chave secreta e cria a chave de assinatura simétrica.
    /// </summary>
    /// <param name="configuration">Configuração da aplicação contendo parâmetros JWT</param>
    /// <exception cref="ArgumentNullException">Se a configuração JWT:Secret não estiver presente</exception>
    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        var secret = _configuration["Jwt:Secret"] 
            ?? throw new ArgumentNullException("Jwt:Secret", "JWT Secret não configurado");
        
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    /// <summary>
    /// Gera um token JWT para o usuário fornecido.
    /// O token inclui claims obrigatórios: NameIdentifier, Email, e Jti.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    /// <param name="user">Usuário para o qual o token será gerado</param>
    /// <returns>String contendo o token JWT codificado</returns>
    /// <exception cref="ArgumentNullException">Se o usuário for nulo</exception>
    public string GenerateToken(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        // Criar claims conforme especificação: NameIdentifier, Email, Jti
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Configurar credenciais de assinatura com algoritmo HmacSha256
        var credentials = new SigningCredentials(
            _key,
            SecurityAlgorithms.HmacSha256);

        // Obter tempo de expiração da configuração
        var expirationMinutes = int.Parse(
            _configuration["Jwt:ExpirationMinutes"] ?? "60");

        // Criar o token JWT
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        // Retornar token serializado
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Valida um token JWT verificando assinatura, issuer, audience, expiração e algoritmo.
    /// Garante que apenas tokens assinados com HmacSha256 sejam aceitos.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    /// <param name="token">Token JWT a ser validado</param>
    /// <returns>ClaimsPrincipal se o token for válido; null caso contrário</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();

        // Configurar parâmetros de validação conforme Requirements 1.4, 1.5
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = _key,
            ClockSkew = TimeSpan.Zero // Remove tolerância padrão de 5 minutos
        };

        try
        {
            // Validar o token
            var principal = tokenHandler.ValidateToken(
                token,
                validationParameters,
                out var validatedToken);

            // Verificar se o algoritmo de assinatura é HmacSha256
            // Rejeitar tokens assinados com algoritmos diferentes
            return validatedToken is JwtSecurityToken jwtToken
                && jwtToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase)
                ? principal
                : null;
        }
        catch (SecurityTokenExpiredException)
        {
            // Token expirado - retornar null
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            // Assinatura inválida - retornar null
            return null;
        }
        catch (Exception)
        {
            // Qualquer outro erro de validação - retornar null
            return null;
        }
    }
}
