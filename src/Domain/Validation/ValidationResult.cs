namespace Domain.Validation;

/// <summary>
/// Representa o resultado de uma operação de validação.
/// Contém status de sucesso/falha e lista de erros de validação.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indica se a validação foi bem-sucedida
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Lista de chaves de validação que falharam
    /// </summary>
    public List<ValidationKey> Errors { get; }

    private ValidationResult(bool isValid, List<ValidationKey> errors)
    {
        IsValid = isValid;
        Errors = errors ?? new List<ValidationKey>();
    }

    /// <summary>
    /// Cria um resultado de validação bem-sucedido sem erros
    /// </summary>
    /// <returns>ValidationResult indicando sucesso</returns>
    public static ValidationResult Success() =>
        new ValidationResult(true, new List<ValidationKey>());

    /// <summary>
    /// Cria um resultado de validação falhado com erros específicos
    /// </summary>
    /// <param name="errors">Array de chaves de validação que falharam</param>
    /// <returns>ValidationResult indicando falha com erros</returns>
    public static ValidationResult Failure(params ValidationKey[] errors) =>
        new ValidationResult(false, errors.ToList());

    /// <summary>
    /// Combina múltiplos resultados de validação em um único resultado.
    /// Se algum resultado contiver erros, o resultado combinado será inválido.
    /// </summary>
    /// <param name="results">Array de resultados a serem combinados</param>
    /// <returns>ValidationResult combinado</returns>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var allErrors = results.SelectMany(r => r.Errors).ToList();
        return allErrors.Any()
            ? new ValidationResult(false, allErrors)
            : Success();
    }

    /// <summary>
    /// Adiciona erros de outro ValidationResult a este resultado
    /// </summary>
    /// <param name="other">Outro ValidationResult para combinar</param>
    /// <returns>Novo ValidationResult com erros combinados</returns>
    public ValidationResult WithErrors(ValidationResult other)
    {
        if (other.IsValid)
            return this;

        var combinedErrors = Errors.Concat(other.Errors).ToList();
        return new ValidationResult(false, combinedErrors);
    }

    /// <summary>
    /// Adiciona um único erro ao resultado
    /// </summary>
    /// <param name="error">Chave de validação a ser adicionada</param>
    /// <returns>Novo ValidationResult com erro adicionado</returns>
    public ValidationResult WithError(ValidationKey error)
    {
        var newErrors = Errors.ToList();
        newErrors.Add(error);
        return new ValidationResult(false, newErrors);
    }
}
