using Domain.Validation;

namespace Domain.Entities;

/// <summary>
/// Entidade de domínio representando uma lista de busca contendo palavras-chave e domínios a serem monitorados.
/// Estende BaseEntity e implementa auto-validação através de Map/Ensure.
/// **Validates: Requirements 3.2, 3.3, 8.3**
/// </summary>
public class SearchList : BaseEntity, IMapEnsure<SearchList>
{
    private const int MaxNameLength = 100;
    private const int MinNameLength = 3;

    /// <summary>
    /// Nome da lista de busca
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Lista de palavras-chave a serem monitoradas
    /// </summary>
    public List<string> Keywords { get; private set; } = new List<string>();

    /// <summary>
    /// Lista de domínios a serem monitorados
    /// </summary>
    public List<string> Domains { get; private set; } = new List<string>();

    /// <summary>
    /// ID do usuário proprietário desta lista
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Construtor privado para EF Core
    /// </summary>
    private SearchList()
    {
    }

    /// <summary>
    /// Factory method com validação para criar uma nova SearchList.
    /// Executa Map e Ensure antes de retornar a instância.
    /// </summary>
    /// <param name="name">Nome da lista</param>
    /// <param name="keywords">Lista de palavras-chave</param>
    /// <param name="domains">Lista de domínios</param>
    /// <param name="userId">ID do usuário proprietário</param>
    /// <returns>Result contendo o SearchList criado ou erros de validação</returns>
    public static Result<SearchList> Create(
        string name,
        List<string> keywords,
        List<string> domains,
        Guid userId)
    {
        var searchList = new SearchList
        {
            Name = name?.Trim() ?? string.Empty,
            Keywords = keywords ?? new List<string>(),
            Domains = domains ?? new List<string>(),
            UserId = userId
        };

        var mapResult = Map(searchList);
        if (!mapResult.IsValid)
            return Result<SearchList>.Failure(mapResult.Errors);

        var ensureResult = Ensure(searchList);
        if (!ensureResult.IsValid)
            return Result<SearchList>.Failure(ensureResult.Errors);

        return Result<SearchList>.Success(searchList);
    }

    /// <summary>
    /// Realiza validação estrutural básica da SearchList.
    /// Verifica campos obrigatórios, tipos corretos e limites de comprimento.
    /// **Validates: Requirements 3.2, 8.3**
    /// </summary>
    /// <param name="value">SearchList a ser validada</param>
    /// <returns>Resultado da validação com possíveis erros</returns>
    public static ValidationResult Map(SearchList value)
    {
        var errors = new List<ValidationKey>();

        // Nome obrigatório
        if (string.IsNullOrWhiteSpace(value.Name))
            errors.Add(ValidationKey.Required("search_list", "name"));

        // Nome não pode exceder comprimento máximo
        if (value.Name?.Length > MaxNameLength)
            errors.Add(ValidationKey.MaxLength("search_list", "name"));

        // Keywords obrigatória e deve ter pelo menos um item
        if (value.Keywords == null || !value.Keywords.Any())
            errors.Add(ValidationKey.Required("search_list", "keywords"));

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Realiza validação de regras de negócio e invariantes.
    /// Verifica restrições de domínio, relações e estado consistente.
    /// **Validates: Requirements 3.3, 8.3**
    /// </summary>
    /// <param name="value">SearchList a ser validada</param>
    /// <returns>Resultado da validação com possíveis erros</returns>
    public static ValidationResult Ensure(SearchList value)
    {
        var errors = new List<ValidationKey>();

        // Nome deve ter comprimento mínimo
        if (!string.IsNullOrWhiteSpace(value.Name) && value.Name.Length < MinNameLength)
            errors.Add(ValidationKey.MinLength("search_list", "name"));

        // Keywords não pode conter strings vazias
        if (value.Keywords != null && value.Keywords.Any(k => string.IsNullOrWhiteSpace(k)))
            errors.Add(ValidationKey.Custom("search_list", "keywords", "contains_empty"));

        // Domains não pode conter strings vazias (se houver domínios)
        if (value.Domains != null && value.Domains.Any(d => string.IsNullOrWhiteSpace(d)))
            errors.Add(ValidationKey.Custom("search_list", "domains", "contains_empty"));

        // UserId não pode ser vazio
        if (value.UserId == Guid.Empty)
            errors.Add(ValidationKey.Custom("search_list", "user_id", "invalid"));

        return errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Atualiza os dados da SearchList com novos valores.
    /// Atualiza o timestamp UpdatedAt automaticamente.
    /// </summary>
    /// <param name="name">Novo nome</param>
    /// <param name="keywords">Nova lista de palavras-chave</param>
    /// <param name="domains">Nova lista de domínios</param>
    public void Update(string name, List<string> keywords, List<string> domains)
    {
        Name = name?.Trim() ?? string.Empty;
        Keywords = keywords ?? new List<string>();
        Domains = domains ?? new List<string>();
        Touch();
    }
}
