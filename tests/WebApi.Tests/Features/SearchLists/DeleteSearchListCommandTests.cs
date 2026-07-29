using Domain.Entities;
using Infrastructure.Persistence;
using WebApi.Features.SearchLists;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.SearchLists;

/// <summary>
/// Testes de integração para DeleteSearchListCommand.
/// **Property 8: SearchList Deletion Removes Entity**
/// **Valida: Requirements 3.8**
/// </summary>
public class DeleteSearchListCommandTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public DeleteSearchListCommandTests()
    {
        _context = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que DeleteSearchList remove a entidade do banco de dados.
    /// **Validates: Requirement 3.8**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldRemoveEntity_FromDatabase()
    {
        // Arrange - Create a SearchList first
        var userId = Guid.NewGuid();
        var createResult = SearchList.Create(
            "Test List",
            new List<string> { "keyword1" },
            new List<string>(),
            userId);
        Assert.True(createResult.IsSuccess);

        _context.SearchLists.Add(createResult.Value);
        await _context.SaveChangesAsync();

        var searchListId = createResult.Value.Id;
        var command = new DeleteSearchListCommand(searchListId);
        var handler = new DeleteSearchListHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        // Verify deletion
        var deletedSearchList = await _context.SearchLists.FindAsync(searchListId);
        Assert.Null(deletedSearchList);
    }

    /// <summary>
    /// Testa que DeleteSearchList retorna falha quando a entidade não existe.
    /// **Validates: Requirement 3.8**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenEntityNotFound()
    {
        // Arrange
        var command = new DeleteSearchListCommand(Guid.NewGuid());
        var handler = new DeleteSearchListHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key.Contains("not_found"));
    }

    /// <summary>
    /// Testa que DeleteSearchList não afeta outras entidades.
    /// **Validates: Requirement 3.8**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotAffectOtherEntities()
    {
        // Arrange - Create two SearchLists
        var userId = Guid.NewGuid();
        var list1 = SearchList.Create("List 1", new List<string> { "k1" }, new List<string>(), userId);
        var list2 = SearchList.Create("List 2", new List<string> { "k2" }, new List<string>(), userId);
        Assert.True(list1.IsSuccess);
        Assert.True(list2.IsSuccess);

        _context.SearchLists.Add(list1.Value);
        _context.SearchLists.Add(list2.Value);
        await _context.SaveChangesAsync();

        // Act - Delete only list1
        var command = new DeleteSearchListCommand(list1.Value.Id);
        var handler = new DeleteSearchListHandler(_context);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        // Verify list2 still exists
        var remainingList = await _context.SearchLists.FindAsync(list2.Value.Id);
        Assert.NotNull(remainingList);
        Assert.Equal("List 2", remainingList.Name);
    }
}
