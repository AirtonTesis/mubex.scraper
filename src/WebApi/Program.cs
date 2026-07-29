using System.Text;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using Infrastructure.Queue;
using Infrastructure.Scraping;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WebApi;

var builder = WebApplication.CreateBuilder(args);

// Configure Entity Framework Core with SQLite for local development
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Configure MediatR for CQRS
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Register Infrastructure services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Usar InMemoryQueueManager em desenvolvimento (não requer RabbitMQ)
// Para produção, substituir por RabbitMqQueueManager
builder.Services.AddSingleton<IQueueManager, InMemoryQueueManager>();

// Registrar serviços de scraping real com Playwright
builder.Services.AddSingleton<IUserAgentRotationService, UserAgentRotationService>();
builder.Services.AddSingleton<ICaptchaDetectionService, CaptchaDetectionService>();
builder.Services.AddSingleton<IHumanClickService, HumanClickService>();
builder.Services.AddSingleton<IImageClassifier, MobileNetClassifier>();
builder.Services.AddHttpClient("CaptchaWebhook", client =>
{
    var webhookUrl = builder.Configuration["CaptchaWebhook:Url"]
        ?? "https://n8n.mubex.app/webhook/captcha-interpreter";
    client.BaseAddress = new Uri(webhookUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IScrapingEngine, PlaywrightScrapingEngine>();

// Registrar o Worker de scraping como BackgroundService dentro da WebApi
builder.Services.AddHostedService<ScrapingBackgroundWorker>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class public for WebApplicationFactory in tests
public partial class Program { }
