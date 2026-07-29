using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using WebApi.Features.SearchLists;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.SearchLists;

/// <summary>
/// Testes de integração para CreateSearchListCommand.
/// **Property 7: SearchList Persistence Round-Trip**
/// **Valida: Requirements 3.4**
/// </summary>
public class CreateSearchListCommandTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public CreateSearchListCommandTests()
    {
        _context = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que CreateSearchList salva a SearchList no banco de dados
    /// e pode ser recuperada posteriormente (round-trip).
    /// **Validates: Requirement 3.4**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldPersistSearchList_InDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateSearchListCommand(
            Name: "Test Search List",
            Keywords: new List<string> { "keyword1", "keyword2" },
            Domains: new List<string> { "example.com" },
            UserId: userId);

        var handler = new CreateSearchListHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        // Verify persistence - retrieve from database
        var savedSearchList = await _context.SearchLists.FindAsync(result.Value);
        Assert.NotNull(savedSearchList);
        Assert.Equal("Test Search List", savedSearchList.Name);
        Assert.Equal(userId, savedSearchList.UserId);
        Assert.Equal(2, savedSearchList.Keywords.Count);
        Assert.Contains("keyword1", savedSearchList.Keywords);
        Assert.Contains("keyword2", savedSearchList.Keywords);
        Assert.Single(savedSearchList.Domains);
        Assert.Contains("example.com", savedSearchList.Domains);
    }

    /// <summary>
    /// Testa que CreateSearchList retorna falha com erros de validação
    /// quando os dados são inválidos.
    /// **Validates: Requirements 3.2, 3.3**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenValidationFails()
    {
        // Arrange - Name is empty, Keywords is empty (both required)
        var command = new CreateSearchListCommand(
            Name: "",
            Keywords: new List<string>(),
            Domains: new List<string>(),
            UserId: Guid.NewGuid());

        var handler = new CreateSearchListHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>
    /// Testa que CreateSearchList com Keywords vazias retorna erro de validação.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenKeywordsEmpty()
    {
        // Arrange
        var command = new CreateSearchListCommand(
            Name: "Valid Name",
            Keywords: new List<string>(),
            Domains: new List<string>(),
            UserId: Guid.NewGuid());

        var handler = new CreateSearchListHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key.Contains("keywords"));
    }

    /// <summary>
    /// Testa que CreateSearchList com nome muito curto retorna erro de validação.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenNameTooShort()
    {
        // Arrange - Name must be at least 3 characters
        var command = new CreateSearchListCommand(
            Name: "ab",
            Keywords: new List<string> { "keyword1" },
            Domains: new List<string>(),
            UserId: Guid.NewGuid());

        var handler = new CreateSearchListHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key.Contains("name"));
    }
}
