using Domain.Validation;
using Xunit;

namespace Domain.Tests.Validation;

/// <summary>
/// Testes unitários para ValidationResult
/// Valida criação, combinação e manipulação de resultados de validação
/// **Validates: Requirements 8.3, 8.4**
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void Success_CreatesValidResult()
    {
        // Arrange & Act
        var result = ValidationResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_WithSingleError_CreatesInvalidResult()
    {
        // Arrange
        var error = ValidationKey.Required("search_list", "name");

        // Act
        var result = ValidationResult.Failure(error);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains(error, result.Errors);
    }

    [Fact]
    public void Failure_WithMultipleErrors_CreatesInvalidResult()
    {
        // Arrange
        var error1 = ValidationKey.Required("search_list", "name");
        var error2 = ValidationKey.MaxLength("search_list", "name");

        // Act
        var result = ValidationResult.Failure(error1, error2);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(error1, result.Errors);
        Assert.Contains(error2, result.Errors);
    }

    [Fact]
    public void Combine_WithAllSuccessResults_ReturnsSuccess()
    {
        // Arrange
        var result1 = ValidationResult.Success();
        var result2 = ValidationResult.Success();
        var result3 = ValidationResult.Success();

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.True(combined.IsValid);
        Assert.Empty(combined.Errors);
    }

    [Fact]
    public void Combine_WithOneFailure_ReturnsFailure()
    {
        // Arrange
        var result1 = ValidationResult.Success();
        var result2 = ValidationResult.Failure(ValidationKey.Required("entity", "field"));
        var result3 = ValidationResult.Success();

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.False(combined.IsValid);
        Assert.Single(combined.Errors);
    }

    [Fact]
    public void Combine_WithMultipleFailures_AggregatesAllErrors()
    {
        // Arrange
        var error1 = ValidationKey.Required("search_list", "name");
        var error2 = ValidationKey.MinLength("search_list", "name");
        var error3 = ValidationKey.Required("search_list", "keywords");

        var result1 = ValidationResult.Failure(error1, error2);
        var result2 = ValidationResult.Failure(error3);

        // Act
        var combined = ValidationResult.Combine(result1, result2);

        // Assert
        Assert.False(combined.IsValid);
        Assert.Equal(3, combined.Errors.Count);
        Assert.Contains(error1, combined.Errors);
        Assert.Contains(error2, combined.Errors);
        Assert.Contains(error3, combined.Errors);
    }

    [Fact]
    public void WithErrors_WhenOtherIsValid_ReturnsOriginalResult()
    {
        // Arrange
        var error = ValidationKey.Required("entity", "field");
        var result = ValidationResult.Failure(error);
        var otherResult = ValidationResult.Success();

        // Act
        var combined = result.WithErrors(otherResult);

        // Assert
        Assert.False(combined.IsValid);
        Assert.Single(combined.Errors);
        Assert.Contains(error, combined.Errors);
    }

    [Fact]
    public void WithErrors_WhenOtherHasErrors_CombinesErrors()
    {
        // Arrange
        var error1 = ValidationKey.Required("entity", "field1");
        var error2 = ValidationKey.Required("entity", "field2");
        
        var result1 = ValidationResult.Failure(error1);
        var result2 = ValidationResult.Failure(error2);

        // Act
        var combined = result1.WithErrors(result2);

        // Assert
        Assert.False(combined.IsValid);
        Assert.Equal(2, combined.Errors.Count);
        Assert.Contains(error1, combined.Errors);
        Assert.Contains(error2, combined.Errors);
    }

    [Fact]
    public void WithError_AddsErrorToResult()
    {
        // Arrange
        var error1 = ValidationKey.Required("entity", "field1");
        var error2 = ValidationKey.MaxLength("entity", "field2");
        
        var result = ValidationResult.Failure(error1);

        // Act
        var updated = result.WithError(error2);

        // Assert
        Assert.False(updated.IsValid);
        Assert.Equal(2, updated.Errors.Count);
        Assert.Contains(error1, updated.Errors);
        Assert.Contains(error2, updated.Errors);
    }

    [Fact]
    public void WithError_OnSuccessResult_CreatesFailureResult()
    {
        // Arrange
        var result = ValidationResult.Success();
        var error = ValidationKey.Required("entity", "field");

        // Act
        var updated = result.WithError(error);

        // Assert
        Assert.False(updated.IsValid);
        Assert.Single(updated.Errors);
        Assert.Contains(error, updated.Errors);
    }

    [Fact]
    public void ValidationResult_IsImmutable()
    {
        // Arrange
        var originalError = ValidationKey.Required("entity", "field1");
        var result = ValidationResult.Failure(originalError);
        var newError = ValidationKey.MaxLength("entity", "field2");

        // Act
        var updated = result.WithError(newError);

        // Assert - Original result should be unchanged
        Assert.Single(result.Errors);
        Assert.Contains(originalError, result.Errors);
        
        // Updated result should have both errors
        Assert.Equal(2, updated.Errors.Count);
        Assert.Contains(originalError, updated.Errors);
        Assert.Contains(newError, updated.Errors);
    }
}
