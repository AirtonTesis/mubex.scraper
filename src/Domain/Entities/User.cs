using Domain.Validation;

namespace Domain.Entities;

/// <summary>
/// Entidade de domínio representando um usuário autenticado na plataforma.
/// Estende BaseEntity e implementa auto-validação através de Map/Ensure.
/// **Validates: Requirements 1.1**
/// </summary>
public class User : BaseEntity, IMapEnsure<User>
{
    private const int MaxEmailLength = 255;
    private const int MinPasswordHashLength = 60; // BCrypt hash típico tem 60 caracteres

    /// <summary>
    /// Email do usuário (deve ser único no sistema)
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Hash da senha do usuário (nunca armazena senha em texto plano)
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Construtor privado para EF Core
    /// </summary>
    private User()
    {
    }

    /// <summary>
    /// Factory method com validação para criar um novo usuário.
    /// Executa Map e Ensure antes de retornar a instância.
    /// </summary>
    /// <param name="email">Email do usuário</param>
    /// <param name="passwordHash">Hash da senha</param>
    /// <returns>Result contendo o User criado ou erros de validação</returns>
    public static Result<User> Create(string email, string passwordHash)
    {
        var user = new User
        {
            Email = email?.Trim().ToLowerInvariant() ?? string.Empty,
            PasswordHash = passwordHash ?? string.Empty
        };

        var mapResult = Map(user);
        if (!mapResult.IsValid)
            return Result<User>.Failure(mapResult.Errors);

        var ensureResult = Ensure(user);
        if (!ensureResult.IsValid)
            return Result<User>.Failure(ensureResult.Errors);

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Realiza validação estrutural básica do usuário.
    /// Verifica campos obrigatórios e limites de comprimento.
    /// **Validates: Requirements 8.3**
    /// </summary>
    /// <param name="value">Usuário a ser validado</param>
    /// <returns>Resultado da validação com possíveis erros</returns>
    public static ValidationResult Map(User value)
    {
        var errors = new List<ValidationKey>();

        // Email obrigatório
        if (string.IsNullOrWhiteSpace(value.Email))
            errors.Add(ValidationKey.Required("user", "email"));

        // Email não pode exceder comprimento máximo
        if (value.Email?.Length > MaxEmailLength)
            errors.Add(ValidationKey.MaxLength("user", "email"));

        // PasswordHash obrigatório
        if (string.IsNullOrWhiteSpace(value.PasswordHash))
            errors.Add(ValidationKey.Required("user", "password_hash"));

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Realiza validação de regras de negócio e invariantes.
    /// Verifica formato de email e integridade do hash de senha.
    /// **Validates: Requirements 8.3**
    /// </summary>
    /// <param name="value">Usuário a ser validado</param>
    /// <returns>Resultado da validação com possíveis erros</returns>
    public static ValidationResult Ensure(User value)
    {
        var errors = new List<ValidationKey>();

        // Validar formato de email
        if (!string.IsNullOrWhiteSpace(value.Email) && !IsValidEmail(value.Email))
            errors.Add(ValidationKey.InvalidFormat("user", "email"));

        // Validar comprimento mínimo do hash (garantia de que é realmente um hash)
        if (!string.IsNullOrWhiteSpace(value.PasswordHash) && 
            value.PasswordHash.Length < MinPasswordHashLength)
            errors.Add(ValidationKey.MinLength("user", "password_hash"));

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Atualiza o hash da senha do usuário
    /// </summary>
    /// <param name="passwordHash">Novo hash de senha</param>
    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Touch();
    }

    /// <summary>
    /// Valida formato básico de email usando regex simples
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            // Validação básica de formato de email
            var emailRegex = new System.Text.RegularExpressions.Regex(
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            return emailRegex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Classe auxiliar para encapsular resultados de validação com valor tipado.
/// Usado pelos factory methods das entidades.
/// </summary>
/// <typeparam name="T">Tipo da entidade</typeparam>
public class Result<T>
{
    /// <summary>
    /// Indica se a operação foi bem-sucedida
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Valor da entidade (apenas se IsSuccess = true)
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Lista de erros de validação (apenas se IsSuccess = false)
    /// </summary>
    public List<ValidationKey> Errors { get; }

    private Result(bool isSuccess, T value, List<ValidationKey> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors ?? new List<ValidationKey>();
    }

    /// <summary>
    /// Cria um resultado de sucesso com valor
    /// </summary>
    public static Result<T> Success(T value) =>
        new Result<T>(true, value, new List<ValidationKey>());

    /// <summary>
    /// Cria um resultado de falha com erros
    /// </summary>
    public static Result<T> Failure(List<ValidationKey> errors) =>
        new Result<T>(false, default!, errors);
}
