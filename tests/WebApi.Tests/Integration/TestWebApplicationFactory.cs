using System.Text;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace WebApi.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add InMemory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString()));
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Jwt:Secret", "test-secret-key-min-32-characters-long-for-hmacsha256" },
                { "Jwt:Issuer", "test-issuer" },
                { "Jwt:Audience", "test-audience" },
                { "Jwt:ExpirationMinutes", "60" },
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=test" }
            });
        });
    }

    public static string GenerateTestToken(Guid userId, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-min-32-characters-long-for-hmacsha256"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
