namespace Domain.Validation;

/// <summary>
/// Interface para auto-validação de entidades de domínio utilizando os métodos Map e Ensure.
/// Map: Validação estrutural básica (campos obrigatórios, tipos corretos)
/// Ensure: Validação de regras de negócio e invariantes
/// </summary>
/// <typeparam name="T">Tipo da entidade a ser validada</typeparam>
public interface IMapEnsure<T>
{
    /// <summary>
    /// Realiza validação estrutural básica da entidade.
    /// Valida campos obrigatórios, tipos de dados e formato.
    /// </summary>
    /// <param name="value">Entidade a ser validada</param>
    /// <returns>Resultado da validação com possíveis erros</returns>
    static abstract ValidationResult Map(T value);

    /// <summary>
    /// Realiza validação de regras de negócio e invariantes.
    /// Valida restrições de domínio, relações e estado consistente.
    /// </summary>
    /// <param name="value">Entidade a ser validada</param>
    /// <returns>Resultado da validação com possíveis erros</returns>
    static abstract ValidationResult Ensure(T value);
}
