using Domain.Validation;
using Xunit;

namespace Domain.Tests.Validation;

/// <summary>
/// Testes para validar a interface IMapEnsure e sua implementação
/// Cria uma entidade de teste para validar o padrão Map/Ensure
/// **Validates: Requirements 8.3, 8.4**
/// </summary>
public class IMapEnsureTests
{
    // Entidade de teste para demonstrar implementação de IMapEnsure
    private class TestEntity : IMapEnsure<TestEntity>
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }

        public static ValidationResult Map(TestEntity value)
        {
            var errors = new List<ValidationKey>();

            // Validação estrutural
            if (string.IsNullOrWhiteSpace(value.Name))
                errors.Add(ValidationKey.Required("test_entity", "name"));

            if (value.Name?.Length > 50)
                errors.Add(ValidationKey.MaxLength("test_entity", "name"));

            return errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }

        public static ValidationResult Ensure(TestEntity value)
        {
            var errors = new List<ValidationKey>();

            // Regras de negócio
            if (value.Name?.Length < 3)
                errors.Add(ValidationKey.MinLength("test_entity", "name"));

            if (value.Age < 0 || value.Age > 150)
                errors.Add(ValidationKey.OutOfRange("test_entity", "age"));

            return errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }
    }

    [Fact]
    public void Map_WithValidEntity_ReturnsSuccess()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "Valid Name",
            Age = 25
        };

        // Act
        var result = TestEntity.Map(entity);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Map_WithEmptyName_ReturnsRequiredError()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "",
            Age = 25
        };

        // Act
        var result = TestEntity.Map(entity);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("validation.test_entity.name_required", result.Errors[0].Key);
    }

    [Fact]
    public void Map_WithNameTooLong_ReturnsMaxLengthError()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = new string('a', 51),
            Age = 25
        };

        // Act
        var result = TestEntity.Map(entity);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("validation.test_entity.name_max_length", result.Errors[0].Key);
    }

    [Fact]
    public void Ensure_WithValidEntity_ReturnsSuccess()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "Valid Name",
            Age = 25
        };

        // Act
        var result = TestEntity.Ensure(entity);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Ensure_WithNameTooShort_ReturnsMinLengthError()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "Ab",
            Age = 25
        };

        // Act
        var result = TestEntity.Ensure(entity);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("validation.test_entity.name_min_length", result.Errors[0].Key);
    }

    [Fact]
    public void Ensure_WithInvalidAge_ReturnsOutOfRangeError()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "Valid Name",
            Age = 200
        };

        // Act
        var result = TestEntity.Ensure(entity);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("validation.test_entity.age_out_of_range", result.Errors[0].Key);
    }

    [Fact]
    public void MapAndEnsure_Combined_ValidatesCompletely()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = "Ab", // Passa Map mas falha Ensure (muito curto)
            Age = 25
        };

        // Act
        var mapResult = TestEntity.Map(entity);
        var ensureResult = TestEntity.Ensure(entity);
        var combinedResult = ValidationResult.Combine(mapResult, ensureResult);

        // Assert
        Assert.True(mapResult.IsValid); // Map passa
        Assert.False(ensureResult.IsValid); // Ensure falha
        Assert.False(combinedResult.IsValid); // Combinado falha
        Assert.Single(combinedResult.Errors);
        Assert.Equal("validation.test_entity.name_min_length", combinedResult.Errors[0].Key);
    }

    [Fact]
    public void MapAndEnsure_WithMultipleErrors_AggregatesAllErrors()
    {
        // Arrange
        var entity = new TestEntity
        {
            Name = new string('a', 51), // Falha Map (max_length)
            Age = -1   // Falha Ensure (out of range)
        };

        // Act
        var mapResult = TestEntity.Map(entity);
        var ensureResult = TestEntity.Ensure(entity);
        var combinedResult = ValidationResult.Combine(mapResult, ensureResult);

        // Assert
        Assert.False(mapResult.IsValid);
        Assert.False(ensureResult.IsValid);
        Assert.False(combinedResult.IsValid);
        Assert.Equal(2, combinedResult.Errors.Count);
    }
}
