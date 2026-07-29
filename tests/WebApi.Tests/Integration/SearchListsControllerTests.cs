using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApi.Tests.Integration;

/// <summary>
/// Testes de integração para SearchListsController.
/// Testa autenticação JWT, CRUD endpoints e status codes corretos.
/// **Validates: Requirements 3.1, 3.5, 3.6, 3.7**
/// </summary>
public class SearchListsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private readonly Guid _userId;
    private readonly string _token;

    public SearchListsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _userId = Guid.NewGuid();
        _token = TestWebApplicationFactory.GenerateTestToken(_userId, "test@example.com");
    }

    private void AddAuthorizationHeader()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
    }

    /// <summary>
    /// Testa que GET /api/searchlists retorna 401 sem token.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Fact]
    public async Task GetAll_ShouldReturn401_WithoutToken()
    {
        // Act
        var response = await _client.GetAsync("/api/searchlists");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Testa que POST /api/searchlists retorna 201 com dados válidos.
    /// **Validates: Requirement 3.1**
    /// </summary>
    [Fact]
    public async Task Create_ShouldReturn201_WithValidData()
    {
        // Arrange
        AddAuthorizationHeader();
        var request = new
        {
            Name = "Test Search List",
            Keywords = new List<string> { "keyword1", "keyword2" },
            Domains = new List<string> { "example.com" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/searchlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Testa que POST /api/searchlists retorna 400 com dados inválidos.
    /// **Validates: Requirement 3.2**
    /// </summary>
    [Fact]
    public async Task Create_ShouldReturn400_WithInvalidData()
    {
        // Arrange
        AddAuthorizationHeader();
        var request = new
        {
            Name = "",
            Keywords = new List<string>(),
            Domains = new List<string>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/searchlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Testa que GET /api/searchlists retorna 200 com listas do usuário.
    /// **Validates: Requirement 3.5**
    /// </summary>
    [Fact]
    public async Task GetAll_ShouldReturn200_WithUserLists()
    {
        // Arrange
        AddAuthorizationHeader();

        // Act
        var response = await _client.GetAsync("/api/searchlists");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Testa que DELETE /api/searchlists/{id} retorna 404 quando não existe.
    /// **Validates: Requirement 3.8**
    /// </summary>
    [Fact]
    public async Task Delete_ShouldReturn404_WhenNotFound()
    {
        // Arrange
        AddAuthorizationHeader();

        // Act
        var response = await _client.DeleteAsync($"/api/searchlists/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
