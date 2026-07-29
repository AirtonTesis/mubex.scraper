using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApi.Tests.Integration;

/// <summary>
/// Testes de integração para JobsController.
/// Testa autenticação JWT, endpoints de gerenciamento e status codes corretos.
/// **Validates: Requirements 4.1, 4.4, 4.6, 4.8**
/// </summary>
public class JobsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private readonly Guid _userId;
    private readonly string _token;

    public JobsControllerTests(TestWebApplicationFactory factory)
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
    /// Testa que POST /api/jobs retorna 401 sem token.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Fact]
    public async Task Enqueue_ShouldReturn401_WithoutToken()
    {
        // Arrange
        var request = new { SearchListId = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Testa que POST /api/jobs retorna 400 quando SearchList não existe.
    /// **Validates: Requirement 4.1**
    /// </summary>
    [Fact]
    public async Task Enqueue_ShouldReturn400_WhenSearchListNotFound()
    {
        // Arrange
        AddAuthorizationHeader();
        var request = new { SearchListId = Guid.NewGuid() };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Testa que PATCH /api/jobs/{id}/pause retorna 404 quando job não existe.
    /// **Validates: Requirement 4.4**
    /// </summary>
    [Fact]
    public async Task Pause_ShouldReturn404_WhenJobNotFound()
    {
        // Arrange
        AddAuthorizationHeader();

        // Act
        var response = await _client.PatchAsync($"/api/jobs/{Guid.NewGuid()}/pause", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Testa que PATCH /api/jobs/{id}/activate retorna 404 quando job não existe.
    /// **Validates: Requirement 4.6**
    /// </summary>
    [Fact]
    public async Task Activate_ShouldReturn404_WhenJobNotFound()
    {
        // Arrange
        AddAuthorizationHeader();

        // Act
        var response = await _client.PatchAsync($"/api/jobs/{Guid.NewGuid()}/activate", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Testa que GET /api/jobs/{id}/history retorna 200 com histórico do job.
    /// **Validates: Requirement 4.8**
    /// </summary>
    [Fact]
    public async Task GetHistory_ShouldReturn200_WithJobHistory()
    {
        // Arrange
        AddAuthorizationHeader();

        // Act
        var response = await _client.GetAsync($"/api/jobs/{Guid.NewGuid()}/history");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
