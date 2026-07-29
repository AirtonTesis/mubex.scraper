using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WebApi.Features.SearchLists;
using WebApi.Tests.TestHelpers;
using Xunit;

namespace WebApi.Tests.Features.SearchLists;

/// <summary>
/// Testes de integração para GetAllSearchListsQuery.
/// **Property 22: Read Queries Have No Side Effects**
/// **Valida: Requirements 9.5**
/// </summary>
public class GetAllSearchListsQueryTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public GetAllSearchListsQueryTests()
    {
        _context = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Testa que GetAllSearchLists retorna apenas as listas do usuário especificado.
    /// **Validates: Requirements 3.5, 9.5**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnOnlyUserLists()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        var list1 = SearchList.Create("User1 List", new List<string> { "k1" }, new List<string>(), userId1);
        var list2 = SearchList.Create("User2 List", new List<string> { "k2" }, new List<string>(), userId2);
        var list3 = SearchList.Create("User1 List 2", new List<string> { "k3" }, new List<string>(), userId1);

        _context.SearchLists.Add(list1.Value);
        _context.SearchLists.Add(list2.Value);
        _context.SearchLists.Add(list3.Value);
        await _context.SaveChangesAsync();

        var query = new GetAllSearchListsQuery(userId1);
        var handler = new GetAllSearchListsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, sl => Assert.Equal(userId1, sl.UserId));
    }

    /// <summary>
    /// Testa que GetAllSearchLists não modifica o banco de dados (sem efeitos colaterais).
    /// **Validates: Requirement 9.5**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotModifyDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var list = SearchList.Create("Test List", new List<string> { "k1" }, new List<string>(), userId);
        _context.SearchLists.Add(list.Value);
        await _context.SaveChangesAsync();

        var initialCount = await _context.SearchLists.CountAsync();
        var query = new GetAllSearchListsQuery(userId);
        var handler = new GetAllSearchListsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Database should not be modified
        var finalCount = await _context.SearchLists.CountAsync();
        Assert.Equal(initialCount, finalCount);
    }

    /// <summary>
    /// Testa que GetAllSearchLists retorna lista vazia quando usuário não tem listas.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenUserHasNoLists()
    {
        // Arrange
        var query = new GetAllSearchListsQuery(Guid.NewGuid());
        var handler = new GetAllSearchListsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Testa que GetAllSearchLists retorna DTOs com dados corretos.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnCorrectDtoData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var list = SearchList.Create(
            "Test List",
            new List<string> { "keyword1", "keyword2" },
            new List<string> { "example.com" },
            userId);
        _context.SearchLists.Add(list.Value);
        await _context.SaveChangesAsync();

        var query = new GetAllSearchListsQuery(userId);
        var handler = new GetAllSearchListsHandler(_context);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        var dto = result.First();
        Assert.Equal(list.Value.Id, dto.Id);
        Assert.Equal("Test List", dto.Name);
        Assert.Equal(2, dto.Keywords.Count);
        Assert.Single(dto.Domains);
        Assert.Equal(userId, dto.UserId);
    }
}
