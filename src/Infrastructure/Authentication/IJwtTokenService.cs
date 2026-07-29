using System.Security.Claims;
using Domain.Entities;

namespace Infrastructure.Authentication;

/// <summary>
/// Interface para serviço de geração e validação de tokens JWT.
/// Responsável por autenticação baseada em token conforme Requirement 1.1, 1.2, 1.5.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Gera um token JWT para o usuário fornecido.
    /// O token contém claims: NameIdentifier (user ID), Email, e Jti (token ID único).
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    /// <param name="user">Usuário para o qual o token será gerado</param>
    /// <returns>String contendo o token JWT codificado</returns>
    string GenerateToken(User user);

    /// <summary>
    /// Valida um token JWT verificando assinatura, issuer, audience e expiração.
    /// Garante que o algoritmo de assinatura seja HmacSha256.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    /// <param name="token">Token JWT a ser validado</param>
    /// <returns>ClaimsPrincipal se o token for válido; null caso contrário</returns>
    ClaimsPrincipal? ValidateToken(string token);
}
