using System.Net;
using System.Net.Http.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApi.Tests.Integration;

/// <summary>
/// Testes de integração para DashboardController.
/// Testa autenticação JWT, endpoint de métricas e status codes corretos.
/// **Validates: Requirement 7.1**
/// </summary>
public class DashboardControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private readonly Guid _userId;
    private readonly string _token;

    public DashboardControllerTests(TestWebApplicationFactory factory)
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
    /// Testa que GET /api/dashboard/metrics retorna 401 sem token.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Fact]
    public async Task GetMetrics_ShouldReturn401_WithoutToken()
    {
        // Act
        var response = await _client.GetAsync("/api/dashboard/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Testa que GET /api/dashboard/metrics retorna 200 com parâmetros de data.
    /// **Validates: Requirement 7.1**
    /// </summary>
    [Fact]
    public async Task GetMetrics_ShouldReturn200_WithDateRange()
    {
        // Arrange
        AddAuthorizationHeader();
        var startDate = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/dashboard/metrics?startDate={startDate}&endDate={endDate}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Testa que GET /api/dashboard/metrics retorna 200 sem parâmetros (usa defaults).
    /// **Validates: Requirement 7.1**
    /// </summary>
    [Fact]
    public async Task GetMetrics_ShouldReturn200_WithDefaultDateRange()
    {
        // Arrange
        AddAuthorizationHeader();

        // Act
        var response = await _client.GetAsync("/api/dashboard/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
