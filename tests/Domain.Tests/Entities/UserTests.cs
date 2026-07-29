using Domain.Entities;
using Domain.Validation;

namespace Domain.Tests.Entities;

/// <summary>
/// Testes unitários para a entidade User
/// **Validates: Requirements 1.1**
/// </summary>
public class UserTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_WithValidEmailAndPasswordHash_ShouldReturnSuccess()
    {
        // Arrange
        var email = "user@example.com";
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2"; // BCrypt hash example

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("user@example.com", result.Value.Email);
        Assert.Equal(passwordHash, result.Value.PasswordHash);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.True(result.Value.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.Value.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithEmailInUpperCase_ShouldConvertToLowerCase()
    {
        // Arrange
        var email = "USER@EXAMPLE.COM";
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value.Email);
    }

    [Fact]
    public void Create_WithEmailWithSpaces_ShouldTrimSpaces()
    {
        // Arrange
        var email = "  user@example.com  ";
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value.Email);
    }

    [Fact]
    public void Create_WithNullEmail_ShouldReturnFailure()
    {
        // Arrange
        string? email = null;
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(email!, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_required");
    }

    [Fact]
    public void Create_WithEmptyEmail_ShouldReturnFailure()
    {
        // Arrange
        var email = "";
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_required");
    }

    [Fact]
    public void Create_WithWhitespaceEmail_ShouldReturnFailure()
    {
        // Arrange
        var email = "   ";
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_required");
    }

    [Fact]
    public void Create_WithNullPasswordHash_ShouldReturnFailure()
    {
        // Arrange
        var email = "user@example.com";
        string? passwordHash = null;

        // Act
        var result = User.Create(email, passwordHash!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.password_hash_required");
    }

    [Fact]
    public void Create_WithEmptyPasswordHash_ShouldReturnFailure()
    {
        // Arrange
        var email = "user@example.com";
        var passwordHash = "";

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.password_hash_required");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user")]
    [InlineData("user@.com")]
    [InlineData("user @example.com")]
    public void Create_WithInvalidEmailFormat_ShouldReturnFailure(string invalidEmail)
    {
        // Arrange
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(invalidEmail, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_invalid_format");
    }

    [Fact]
    public void Create_WithTooLongEmail_ShouldReturnFailure()
    {
        // Arrange
        var email = new string('a', 250) + "@example.com"; // > 255 caracteres
        var passwordHash = "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2";

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_max_length");
    }

    [Fact]
    public void Create_WithShortPasswordHash_ShouldReturnFailure()
    {
        // Arrange
        var email = "user@example.com";
        var passwordHash = "short"; // < 60 caracteres

        // Act
        var result = User.Create(email, passwordHash);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.password_hash_min_length");
    }

    #endregion

    #region Map Method Tests

    [Fact]
    public void Map_WithValidUser_ShouldReturnSuccess()
    {
        // Arrange
        var user = User.Create(
            "user@example.com",
            "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2"
        ).Value;

        // Act
        var result = User.Map(user);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Map_WithNullEmail_ShouldReturnRequiredError()
    {
        // Arrange
        var user = User.Create("user@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;
        // Usar reflexão para simular email null (apenas para teste)
        var emailProperty = typeof(User).GetProperty("Email");
        emailProperty?.SetValue(user, null);

        // Act
        var result = User.Map(user);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_required");
    }

    #endregion

    #region Ensure Method Tests

    [Fact]
    public void Ensure_WithValidUser_ShouldReturnSuccess()
    {
        // Arrange
        var user = User.Create(
            "user@example.com",
            "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2"
        ).Value;

        // Act
        var result = User.Ensure(user);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Ensure_WithInvalidEmailFormat_ShouldReturnInvalidFormatError()
    {
        // Arrange
        var user = User.Create("user@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;
        // Usar reflexão para simular email inválido
        var emailProperty = typeof(User).GetProperty("Email");
        emailProperty?.SetValue(user, "invalid-email");

        // Act
        var result = User.Ensure(user);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.email_invalid_format");
    }

    [Fact]
    public void Ensure_WithShortPasswordHash_ShouldReturnMinLengthError()
    {
        // Arrange
        var user = User.Create("user@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;
        // Usar reflexão para simular hash curto
        var passwordProperty = typeof(User).GetProperty("PasswordHash");
        passwordProperty?.SetValue(user, "short");

        // Act
        var result = User.Ensure(user);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "validation.user.password_hash_min_length");
    }

    #endregion

    #region UpdatePasswordHash Method Tests

    [Fact]
    public void UpdatePasswordHash_ShouldUpdatePasswordAndTimestamp()
    {
        // Arrange
        var user = User.Create(
            "user@example.com",
            "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2"
        ).Value;
        var originalUpdatedAt = user.UpdatedAt;
        var newPasswordHash = "$2a$12$NewHashNewHashNewHashNewHashNewHashNewHashNewHashNewHas";

        // Wait a moment to ensure UpdatedAt changes
        System.Threading.Thread.Sleep(10);

        // Act
        user.UpdatePasswordHash(newPasswordHash);

        // Assert
        Assert.Equal(newPasswordHash, user.PasswordHash);
        Assert.True(user.UpdatedAt > originalUpdatedAt);
    }

    #endregion

    #region BaseEntity Integration Tests

    [Fact]
    public void User_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var user1 = User.Create("user1@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;
        var user2 = User.Create("user2@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;

        // Assert
        Assert.NotEqual(user1.Id, user2.Id);
    }

    [Fact]
    public void User_ShouldHaveCreatedAtTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var user = User.Create("user@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(user.CreatedAt >= beforeCreation);
        Assert.True(user.CreatedAt <= afterCreation);
    }

    [Fact]
    public void User_CreatedAtAndUpdatedAt_ShouldBeEqual_OnCreation()
    {
        // Arrange & Act
        var user = User.Create("user@example.com", "$2a$12$KIXqF7VYQh0QVdZXvF4tUeJnPH0pYFKLOcYH7vJ.pN1nQv5YqZ5Z2").Value;

        // Assert
        // Verifica que a diferença é menor que 1 segundo (permite precisão de milissegundos)
        var difference = Math.Abs((user.UpdatedAt - user.CreatedAt).TotalMilliseconds);
        Assert.True(difference < 1000, $"Expected CreatedAt and UpdatedAt to be within 1 second, but difference was {difference}ms");
    }

    #endregion
}
