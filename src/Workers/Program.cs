using Infrastructure.Persistence;
using Infrastructure.Queue;
using Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;
using Workers;

var builder = Host.CreateApplicationBuilder(args);

// Banco SQLite compartilhado com a WebApi
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=../WebApi/scraper.db"));

// Usar InMemoryQueueManager em desenvolvimento (sem RabbitMQ)
builder.Services.AddSingleton<IQueueManager, InMemoryQueueManager>();

// Registrar serviços de scraping (agora em Infrastructure)
builder.Services.AddSingleton<IUserAgentRotationService, UserAgentRotationService>();
builder.Services.AddSingleton<ICaptchaDetectionService, CaptchaDetectionService>();
builder.Services.AddSingleton<IHumanClickService, HumanClickService>();
builder.Services.AddSingleton<IScrapingEngine, PlaywrightScrapingEngine>();

// Registrar o Worker de scraping
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
