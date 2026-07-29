using Domain.Entities;
using Domain.Validation;

namespace Domain.Tests.Entities;

/// <summary>
/// Testes unitários para a entidade SearchList
/// **Validates: Requirements 3.2, 3.3, 8.3**
/// </summary>
public class SearchListTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1", "keyword2", "keyword3" };
        var domains = new List<string> { "example.com", "test.com" };
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("My Search List", result.Value.Name);
        Assert.Equal(3, result.Value.Keywords.Count);
        Assert.Equal(2, result.Value.Domains.Count);
        Assert.Equal(userId, result.Value.UserId);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.True(result.Value.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.Value.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithNameWithSpaces_ShouldTrimSpaces()
    {
        // Arrange
        var name = "  My Search List  ";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("My Search List", result.Value.Name);
    }

    [Fact]
    public void Create_WithEmptyDomainsList_ShouldReturnSuccess()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Domains);
    }

    [Fact]
    public void Create_WithNullName_ShouldReturnFailure()
    {
        // Arrange
        string? name = null;
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name!, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_required");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldReturnFailure()
    {
        // Arrange
        var name = "";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_required");
    }

    [Fact]
    public void Create_WithWhitespaceName_ShouldReturnFailure()
    {
        // Arrange
        var name = "   ";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_required");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldReturnFailure()
    {
        // Arrange
        var name = new string('a', 101); // > 100 caracteres
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_max_length");
    }

    [Fact]
    public void Create_WithTooShortName_ShouldReturnFailure()
    {
        // Arrange
        var name = "ab"; // < 3 caracteres
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_min_length");
    }

    [Fact]
    public void Create_WithNullKeywords_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        List<string>? keywords = null;
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords!, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_required");
    }

    [Fact]
    public void Create_WithEmptyKeywordsList_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string>();
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_required");
    }

    [Fact]
    public void Create_WithEmptyStringInKeywords_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1", "", "keyword3" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_contains_empty");
    }

    [Fact]
    public void Create_WithWhitespaceStringInKeywords_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1", "   ", "keyword3" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_contains_empty");
    }

    [Fact]
    public void Create_WithEmptyStringInDomains_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string> { "example.com", "", "test.com" };
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.domains_contains_empty");
    }

    [Fact]
    public void Create_WithWhitespaceStringInDomains_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string> { "example.com", "   ", "test.com" };
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.domains_contains_empty");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldReturnFailure()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.Empty;

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.user_id_invalid");
    }

    [Fact]
    public void Create_WithMultipleValidationErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var name = new string('a', 101); // Too long (Map error)
        var keywords = new List<string>(); // Empty list (Map error)
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Errors.Count >= 2);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_max_length");
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_required");
    }

    #endregion

    #region Map Method Tests

    [Fact]
    public void Map_WithValidSearchList_ShouldReturnSuccess()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Act
        var result = SearchList.Map(searchList);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Map_WithNullName_ShouldReturnRequiredError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular name null (apenas para teste)
        var nameProperty = typeof(SearchList).GetProperty("Name");
        nameProperty?.SetValue(searchList, null);

        // Act
        var result = SearchList.Map(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_required");
    }

    [Fact]
    public void Map_WithTooLongName_ShouldReturnMaxLengthError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular name muito longo
        var nameProperty = typeof(SearchList).GetProperty("Name");
        nameProperty?.SetValue(searchList, new string('a', 101));

        // Act
        var result = SearchList.Map(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_max_length");
    }

    [Fact]
    public void Map_WithNullKeywords_ShouldReturnRequiredError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular keywords null
        var keywordsProperty = typeof(SearchList).GetProperty("Keywords");
        keywordsProperty?.SetValue(searchList, null);

        // Act
        var result = SearchList.Map(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_required");
    }

    [Fact]
    public void Map_WithEmptyKeywords_ShouldReturnRequiredError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular keywords vazio
        var keywordsProperty = typeof(SearchList).GetProperty("Keywords");
        keywordsProperty?.SetValue(searchList, new List<string>());

        // Act
        var result = SearchList.Map(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_required");
    }

    #endregion

    #region Ensure Method Tests

    [Fact]
    public void Ensure_WithValidSearchList_ShouldReturnSuccess()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Act
        var result = SearchList.Ensure(searchList);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Ensure_WithTooShortName_ShouldReturnMinLengthError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular name curto
        var nameProperty = typeof(SearchList).GetProperty("Name");
        nameProperty?.SetValue(searchList, "ab");

        // Act
        var result = SearchList.Ensure(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.name_min_length");
    }

    [Fact]
    public void Ensure_WithEmptyStringInKeywords_ShouldReturnContainsEmptyError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular keywords com string vazia
        var keywordsProperty = typeof(SearchList).GetProperty("Keywords");
        keywordsProperty?.SetValue(searchList, new List<string> { "keyword1", "", "keyword3" });

        // Act
        var result = SearchList.Ensure(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.keywords_contains_empty");
    }

    [Fact]
    public void Ensure_WithEmptyStringInDomains_ShouldReturnContainsEmptyError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string> { "example.com" },
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular domains com string vazia
        var domainsProperty = typeof(SearchList).GetProperty("Domains");
        domainsProperty?.SetValue(searchList, new List<string> { "example.com", "", "test.com" });

        // Act
        var result = SearchList.Ensure(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.domains_contains_empty");
    }

    [Fact]
    public void Ensure_WithEmptyUserId_ShouldReturnInvalidError()
    {
        // Arrange
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        
        // Usar reflexão para simular userId vazio
        var userIdProperty = typeof(SearchList).GetProperty("UserId");
        userIdProperty?.SetValue(searchList, Guid.Empty);

        // Act
        var result = SearchList.Ensure(searchList);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.search_list.user_id_invalid");
    }

    #endregion

    #region Update Method Tests

    [Fact]
    public void Update_ShouldUpdateNameKeywordsDomainsAndTimestamp()
    {
        // Arrange
        var searchList = SearchList.Create(
            "Original Name",
            new List<string> { "keyword1" },
            new List<string> { "example.com" },
            Guid.NewGuid()
        ).Value;
        var originalUpdatedAt = searchList.UpdatedAt;
        var originalCreatedAt = searchList.CreatedAt;

        // Wait a moment to ensure UpdatedAt changes
        System.Threading.Thread.Sleep(10);

        var newName = "Updated Name";
        var newKeywords = new List<string> { "keyword2", "keyword3", "keyword4" };
        var newDomains = new List<string> { "test.com", "demo.com" };

        // Act
        searchList.Update(newName, newKeywords, newDomains);

        // Assert
        Assert.Equal("Updated Name", searchList.Name);
        Assert.Equal(3, searchList.Keywords.Count);
        Assert.Equal(2, searchList.Domains.Count);
        Assert.Contains("keyword2", searchList.Keywords);
        Assert.Contains("test.com", searchList.Domains);
        Assert.True(searchList.UpdatedAt > originalUpdatedAt);
        Assert.Equal(originalCreatedAt, searchList.CreatedAt); // CreatedAt should not change
    }

    [Fact]
    public void Update_WithNameWithSpaces_ShouldTrimSpaces()
    {
        // Arrange
        var searchList = SearchList.Create(
            "Original Name",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Act
        searchList.Update("  Updated Name  ", new List<string> { "keyword1" }, new List<string>());

        // Assert
        Assert.Equal("Updated Name", searchList.Name);
    }

    [Fact]
    public void Update_WithNullName_ShouldSetEmptyString()
    {
        // Arrange
        var searchList = SearchList.Create(
            "Original Name",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Act
        searchList.Update(null!, new List<string> { "keyword1" }, new List<string>());

        // Assert
        Assert.Equal(string.Empty, searchList.Name);
    }

    [Fact]
    public void Update_WithNullKeywords_ShouldSetEmptyList()
    {
        // Arrange
        var searchList = SearchList.Create(
            "Original Name",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Act
        searchList.Update("Updated Name", null!, new List<string>());

        // Assert
        Assert.Empty(searchList.Keywords);
    }

    [Fact]
    public void Update_WithNullDomains_ShouldSetEmptyList()
    {
        // Arrange
        var searchList = SearchList.Create(
            "Original Name",
            new List<string> { "keyword1" },
            new List<string> { "example.com" },
            Guid.NewGuid()
        ).Value;

        // Act
        searchList.Update("Updated Name", new List<string> { "keyword1" }, null!);

        // Assert
        Assert.Empty(searchList.Domains);
    }

    #endregion

    #region BaseEntity Integration Tests

    [Fact]
    public void SearchList_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var searchList1 = SearchList.Create(
            "List 1",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;
        var searchList2 = SearchList.Create(
            "List 2",
            new List<string> { "keyword2" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Assert
        Assert.NotEqual(searchList1.Id, searchList2.Id);
    }

    [Fact]
    public void SearchList_ShouldHaveCreatedAtTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(searchList.CreatedAt >= beforeCreation);
        Assert.True(searchList.CreatedAt <= afterCreation);
    }

    [Fact]
    public void SearchList_CreatedAtAndUpdatedAt_ShouldBeEqual_OnCreation()
    {
        // Arrange & Act
        var searchList = SearchList.Create(
            "My Search List",
            new List<string> { "keyword1" },
            new List<string>(),
            Guid.NewGuid()
        ).Value;

        // Assert
        // Verifica que a diferença é menor que 1 segundo (permite precisão de milissegundos)
        var difference = Math.Abs((searchList.UpdatedAt - searchList.CreatedAt).TotalMilliseconds);
        Assert.True(difference < 1000, $"Expected CreatedAt and UpdatedAt to be within 1 second, but difference was {difference}ms");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithMaximumNameLength_ShouldReturnSuccess()
    {
        // Arrange
        var name = new string('a', 100); // Exactly 100 characters
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.Name.Length);
    }

    [Fact]
    public void Create_WithMinimumNameLength_ShouldReturnSuccess()
    {
        // Arrange
        var name = "abc"; // Exactly 3 characters
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Name.Length);
    }

    [Fact]
    public void Create_WithSingleKeyword_ShouldReturnSuccess()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1" };
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Keywords);
    }

    [Fact]
    public void Create_WithManyKeywords_ShouldReturnSuccess()
    {
        // Arrange
        var name = "My Search List";
        var keywords = Enumerable.Range(1, 100).Select(i => $"keyword{i}").ToList();
        var domains = new List<string>();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.Keywords.Count);
    }

    [Fact]
    public void Create_WithManyDomains_ShouldReturnSuccess()
    {
        // Arrange
        var name = "My Search List";
        var keywords = new List<string> { "keyword1" };
        var domains = Enumerable.Range(1, 50).Select(i => $"domain{i}.com").ToList();
        var userId = Guid.NewGuid();

        // Act
        var result = SearchList.Create(name, keywords, domains, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.Domains.Count);
    }

    #endregion
}
