using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Scraping;

/// <summary>
/// Interface para cliques humanizados que simulam comportamento real de usuário.
/// </summary>
public interface IHumanClickService
{
    /// <summary>
    /// Move o mouse incrementalmente (5px) até o alvo e clica.
    /// </summary>
    Task ClickHumanizedAsync(IPage page, int targetX, int targetY, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clica em um elemento com comportamento humanizado.
    /// </summary>
    Task ClickElementHumanizedAsync(IPage page, IElementHandle element, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenta clicar em regiões diferentes da tela em incrementos de 5px.
    /// </summary>
    Task ClickAroundAsync(IPage page, int centerX, int centerY, int radius = 50, int attempts = 5, CancellationToken cancellationToken = default);
}

public class HumanClickService : IHumanClickService
{
    private readonly ILogger<HumanClickService> _logger;

    // Configurações de comportamento humano - movimento mais realista
    private const int StepSizeMin = 3;
    private const int StepSizeMax = 12;
    private const int StepDelayMinMs = 5;
    private const int StepDelayMaxMs = 35;
    private const int ClickDelayMinMs = 80;
    private const int ClickDelayMaxMs = 350;

    public HumanClickService(ILogger<HumanClickService> logger)
    {
        _logger = logger;
    }

    public async Task ClickHumanizedAsync(IPage page, int targetX, int targetY, CancellationToken cancellationToken = default)
    {
        // Obter posição atual real do mouse via JavaScript
        var currentX = Random.Shared.Next(200, 800);
        var currentY = Random.Shared.Next(200, 500);

        _logger.LogDebug("Movendo mouse de ({StartX},{StartY}) para ({TargetX},{TargetY})",
            currentX, currentY, targetX, targetY);

        // Gerar pontos de controle para curva de Bezier (movimento não-linear)
        var midX = (currentX + targetX) / 2 + Random.Shared.Next(-80, 81);
        var midY = (currentY + targetY) / 2 + Random.Shared.Next(-60, 61);
        
        // Número de passos baseado na distância (mais passos = mais suave)
        var distance = Math.Sqrt(Math.Pow(targetX - currentX, 2) + Math.Pow(targetY - currentY, 2));
        var steps = Math.Max(8, (int)(distance / 8));
        
        var prevX = currentX;
        var prevY = currentY;

        for (int i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Interpolação quadrática ao longo da curva de Bezier
            var t = (double)i / steps;
            var oneMinusT = 1 - t;
            
            // Bezier quadrático: P = (1-t)²·P0 + 2·(1-t)·t·P1 + t²·P2
            var bezierX = (oneMinusT * oneMinusT * currentX) + (2 * oneMinusT * t * midX) + (t * t * targetX);
            var bezierY = (oneMinusT * oneMinusT * currentY) + (2 * oneMinusT * t * midY) + (t * t * targetY);
            
            // Adicionar micro-tremor humano (mais no início, menos no final)
            var jitter = (1 - t) * 3;
            var moveX = bezierX + Random.Shared.NextDouble() * jitter * 2 - jitter;
            var moveY = bezierY + Random.Shared.NextDouble() * jitter * 2 - jitter;
            
            var intX = (int)Math.Round(moveX);
            var intY = (int)Math.Round(moveY);
            
            // Só mover se a posição mudou
            if (intX != prevX || intY != prevY)
            {
                await page.Mouse.MoveAsync(intX, intY);
                prevX = intX;
                prevY = intY;
            }

            // Delay variável: mais lento no início e fim (aceleração/desaceleração)
            var baseDelay = Random.Shared.Next(StepDelayMinMs, StepDelayMaxMs + 1);
            var speedFactor = 1.0;
            if (t < 0.2) speedFactor = 1.8; // Início lento
            else if (t > 0.85) speedFactor = 1.5; // Fim lento (desaceleração)
            
            await Task.Delay((int)(baseDelay * speedFactor), cancellationToken);
        }

        // Garantir que estamos exatamente no alvo
        await page.Mouse.MoveAsync(targetX, targetY);
        
        // Micro-pausa antes do clique (hesitação humana)
        var hesitation = Random.Shared.Next(30, 150);
        await Task.Delay(hesitation, cancellationToken);

        // Clicar com pressão variável (mousedown + delay + mouseup)
        await page.Mouse.DownAsync();
        await Task.Delay(Random.Shared.Next(50, 120), cancellationToken);
        await page.Mouse.UpAsync();

        _logger.LogDebug("Clique realizado em ({X},{Y}) com hesitação de {Hesitation}ms", targetX, targetY, hesitation);

        // Delay após clique (humano processes what they clicked)
        await Task.Delay(Random.Shared.Next(100, 400), cancellationToken);
    }

    public async Task ClickElementHumanizedAsync(IPage page, IElementHandle element, CancellationToken cancellationToken = default)
    {
        var box = await element.BoundingBoxAsync();
        if (box == null)
        {
            _logger.LogWarning("Não foi possível obter bounding box do elemento");
            return;
        }

        // Clicar em um ponto aleatório dentro do elemento (não exatamente no centro)
        var targetX = (int)(box.X + box.Width * Random.Shared.NextDouble() * 0.6 + box.Width * 0.2);
        var targetY = (int)(box.Y + box.Height * Random.Shared.NextDouble() * 0.6 + box.Height * 0.2);

        await ClickHumanizedAsync(page, targetX, targetY, cancellationToken);
    }

    public async Task ClickAroundAsync(IPage page, int centerX, int centerY, int radius = 50, int attempts = 5, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < attempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Gerar posição aleatória ao redor do centro com incrementos de 5px
            var angle = Random.Shared.NextDouble() * 2 * Math.PI;
            var distance = Random.Shared.Next(5, radius + 1);

            // Arredondar para múltiplos de 5 (incrementos de 5px)
            var offsetX = (int)(Math.Cos(angle) * distance);
            var offsetY = (int)(Math.Sin(angle) * distance);

            // Garantir incrementos de 5px
            offsetX = (offsetX / 5) * 5;
            offsetY = (offsetY / 5) * 5;

            var targetX = centerX + offsetX;
            var targetY = centerY + offsetY;

            _logger.LogDebug("Clique {Attempt}/{Max} em região ({X},{Y}) (offset: {OffX},{OffY})",
                i + 1, attempts, targetX, targetY, offsetX, offsetY);

            await ClickHumanizedAsync(page, targetX, targetY, cancellationToken);

            // Delay entre tentativas (500ms - 1.5s)
            await Task.Delay(Random.Shared.Next(500, 1500), cancellationToken);
        }
    }
}
