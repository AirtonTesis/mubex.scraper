namespace Domain.Validation;

/// <summary>
/// Representa uma chave de tradução estruturada para erros de validação.
/// Segue o padrão: validation.{entity_name}.{field_name}_{error_type}
/// Permite internacionalização no frontend através de chaves estruturadas.
/// </summary>
public record ValidationKey
{
    /// <summary>
    /// Chave estruturada de validação para tradução no frontend
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Construtor principal para criar uma chave de validação estruturada
    /// </summary>
    /// <param name="entityName">Nome da entidade (ex: search_list)</param>
    /// <param name="fieldName">Nome do campo (ex: name, keywords)</param>
    /// <param name="errorType">Tipo do erro (ex: required, max_length, min_length)</param>
    public ValidationKey(string entityName, string fieldName, string errorType)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            throw new ArgumentException("Entity name cannot be null or empty", nameof(entityName));
        
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
        
        if (string.IsNullOrWhiteSpace(errorType))
            throw new ArgumentException("Error type cannot be null or empty", nameof(errorType));

        Key = $"validation.{entityName.ToLowerInvariant()}.{fieldName.ToLowerInvariant()}_{errorType.ToLowerInvariant()}";
    }

    /// <summary>
    /// Cria uma chave de validação para campo obrigatório
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <returns>ValidationKey com tipo 'required'</returns>
    public static ValidationKey Required(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "required");

    /// <summary>
    /// Cria uma chave de validação para comprimento máximo excedido
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <returns>ValidationKey com tipo 'max_length'</returns>
    public static ValidationKey MaxLength(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "max_length");

    /// <summary>
    /// Cria uma chave de validação para comprimento mínimo não atingido
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <returns>ValidationKey com tipo 'min_length'</returns>
    public static ValidationKey MinLength(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "min_length");

    /// <summary>
    /// Cria uma chave de validação para formato inválido
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <returns>ValidationKey com tipo 'invalid_format'</returns>
    public static ValidationKey InvalidFormat(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "invalid_format");

    /// <summary>
    /// Cria uma chave de validação para valor fora do intervalo permitido
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <returns>ValidationKey com tipo 'out_of_range'</returns>
    public static ValidationKey OutOfRange(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "out_of_range");

    /// <summary>
    /// Cria uma chave de validação para valor duplicado
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <returns>ValidationKey com tipo 'duplicate'</returns>
    public static ValidationKey Duplicate(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "duplicate");

    /// <summary>
    /// Cria uma chave de validação personalizada
    /// </summary>
    /// <param name="entityName">Nome da entidade</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <param name="errorType">Tipo de erro personalizado</param>
    /// <returns>ValidationKey com tipo personalizado</returns>
    public static ValidationKey Custom(string entityName, string fieldName, string errorType) =>
        new ValidationKey(entityName, fieldName, errorType);

    public override string ToString() => Key;
}
