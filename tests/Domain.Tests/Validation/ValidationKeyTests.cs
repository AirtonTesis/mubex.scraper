using Domain.Validation;
using Xunit;

namespace Domain.Tests.Validation;

/// <summary>
/// Testes unitários para ValidationKey
/// Valida criação de chaves estruturadas e métodos auxiliares
/// **Validates: Requirements 8.3, 8.4, 8.5**
/// </summary>
public class ValidationKeyTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesStructuredKey()
    {
        // Arrange & Act
        var key = new ValidationKey("search_list", "name", "required");

        // Assert
        Assert.Equal("validation.search_list.name_required", key.Key);
    }

    [Fact]
    public void Constructor_NormalizesToLowerCase()
    {
        // Arrange & Act
        var key = new ValidationKey("SearchList", "Name", "Required");

        // Assert
        Assert.Equal("validation.searchlist.name_required", key.Key);
    }

    [Theory]
    [InlineData(null, "field", "error")]
    [InlineData("", "field", "error")]
    [InlineData("  ", "field", "error")]
    public void Constructor_WithNullOrEmptyEntityName_ThrowsArgumentException(
        string entityName, string fieldName, string errorType)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ValidationKey(entityName, fieldName, errorType));
        Assert.Contains("Entity name", exception.Message);
    }

    [Theory]
    [InlineData("entity", null, "error")]
    [InlineData("entity", "", "error")]
    [InlineData("entity", "  ", "error")]
    public void Constructor_WithNullOrEmptyFieldName_ThrowsArgumentException(
        string entityName, string fieldName, string errorType)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ValidationKey(entityName, fieldName, errorType));
        Assert.Contains("Field name", exception.Message);
    }

    [Theory]
    [InlineData("entity", "field", null)]
    [InlineData("entity", "field", "")]
    [InlineData("entity", "field", "  ")]
    public void Constructor_WithNullOrEmptyErrorType_ThrowsArgumentException(
        string entityName, string fieldName, string errorType)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ValidationKey(entityName, fieldName, errorType));
        Assert.Contains("Error type", exception.Message);
    }

    [Fact]
    public void Required_CreatesKeyWithRequiredType()
    {
        // Arrange & Act
        var key = ValidationKey.Required("search_list", "name");

        // Assert
        Assert.Equal("validation.search_list.name_required", key.Key);
    }

    [Fact]
    public void MaxLength_CreatesKeyWithMaxLengthType()
    {
        // Arrange & Act
        var key = ValidationKey.MaxLength("search_list", "name");

        // Assert
        Assert.Equal("validation.search_list.name_max_length", key.Key);
    }

    [Fact]
    public void MinLength_CreatesKeyWithMinLengthType()
    {
        // Arrange & Act
        var key = ValidationKey.MinLength("search_list", "name");

        // Assert
        Assert.Equal("validation.search_list.name_min_length", key.Key);
    }

    [Fact]
    public void InvalidFormat_CreatesKeyWithInvalidFormatType()
    {
        // Arrange & Act
        var key = ValidationKey.InvalidFormat("user", "email");

        // Assert
        Assert.Equal("validation.user.email_invalid_format", key.Key);
    }

    [Fact]
    public void OutOfRange_CreatesKeyWithOutOfRangeType()
    {
        // Arrange & Act
        var key = ValidationKey.OutOfRange("job", "retry_count");

        // Assert
        Assert.Equal("validation.job.retry_count_out_of_range", key.Key);
    }

    [Fact]
    public void Duplicate_CreatesKeyWithDuplicateType()
    {
        // Arrange & Act
        var key = ValidationKey.Duplicate("user", "email");

        // Assert
        Assert.Equal("validation.user.email_duplicate", key.Key);
    }

    [Fact]
    public void Custom_CreatesKeyWithCustomErrorType()
    {
        // Arrange & Act
        var key = ValidationKey.Custom("search_list", "keywords", "contains_empty");

        // Assert
        Assert.Equal("validation.search_list.keywords_contains_empty", key.Key);
    }

    [Fact]
    public void ToString_ReturnsKeyString()
    {
        // Arrange
        var key = ValidationKey.Required("search_list", "name");

        // Act
        var result = key.ToString();

        // Assert
        Assert.Equal("validation.search_list.name_required", result);
    }

    [Fact]
    public void ValidationKey_IsRecord_SupportsValueEquality()
    {
        // Arrange
        var key1 = new ValidationKey("search_list", "name", "required");
        var key2 = new ValidationKey("search_list", "name", "required");
        var key3 = new ValidationKey("search_list", "name", "max_length");

        // Act & Assert
        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
    }
}
