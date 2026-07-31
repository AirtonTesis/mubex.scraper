using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Scraping;

/// <summary>
/// Interface para cliques humanizados que simulam comportamento real de usuário.
/// </summary>
public interface IHumanClickService
{
    /// <summary>
    /// Move o mouse incrementalmente até o alvo e clica com comportamento humano realista.
    /// Quando 'fast' é true, usa movimento direto com poucos passos — ideal para cliques
    /// em células de CAPTCHA, onde o desafio expira em ~30s e cada clique precisa ser rápido.
    /// </summary>
    Task ClickHumanizedAsync(IPage page, int targetX, int targetY, CancellationToken cancellationToken = default, bool fast = false);

    /// <summary>
    /// Clica em um elemento com comportamento humanizado.
    /// </summary>
    Task ClickElementHumanizedAsync(IPage page, IElementHandle element, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenta clicar em regiões diferentes da tela com padrões orgânicos.
    /// </summary>
    Task ClickAroundAsync(IPage page, int centerX, int centerY, int radius = 50, int attempts = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simula uma pausa natural onde o usuário "olha" a tela antes de agir.
    /// </summary>
    Task SimulateThinkingAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simula um scroll lento e natural na página.
    /// </summary>
    Task SimulateScrollAsync(IPage page, int deltaY, CancellationToken cancellationToken = default);
}

public class HumanClickService : IHumanClickService
{
    private readonly ILogger<HumanClickService> _logger;

    // Gera um delay com distribuição log-normal (mais realista que uniforme)
    private static int LogNormalDelay(double meanMs, double stdDevMs)
    {
        // Box-Muller transform para gaussiana
        double u1 = 1.0 - Random.Shared.NextDouble();
        double u2 = 1.0 - Random.Shared.NextDouble();
        double gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        // μ = ln(mean²/√(mean²+stdDev²)), σ = √(ln(1+stdDev²/mean²))
        double mu = Math.Log(meanMs * meanMs / Math.Sqrt(meanMs * meanMs + stdDevMs * stdDevMs));
        double sigma = Math.Sqrt(Math.Log(1.0 + stdDevMs * stdDevMs / (meanMs * meanMs)));
        double sample = Math.Exp(mu + sigma * gaussian);
        return Math.Max(20, (int)Math.Round(sample));
    }

    public HumanClickService(ILogger<HumanClickService> logger)
    {
        _logger = logger;
    }

    public async Task ClickHumanizedAsync(IPage page, int targetX, int targetY, CancellationToken cancellationToken = default, bool fast = false)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (fast)
        {
            await FastClickAsync(page, targetX, targetY, cancellationToken);
            return;
        }

        // 1. Simular posição inicial realista (mais variada)
        var currentX = Random.Shared.Next(50, 900);
        var currentY = Random.Shared.Next(100, 700);

        _logger.LogDebug("Movendo mouse de ({StartX},{StartY}) para ({TargetX},{TargetY})",
            currentX, currentY, targetX, targetY);

        // 2. Ocasionalmente, simular "distração" - o usuário olha para outro lugar
        if (Random.Shared.NextDouble() < 0.15)
        {
            var distX = Random.Shared.Next(100, 500);
            var distY = Random.Shared.Next(100, 400);
            await page.Mouse.MoveAsync(distX, distY);
            await Task.Delay(LogNormalDelay(250, 120), cancellationToken);
            // Voltar para o movimento original
        }

        // 3. Múltiplos pontos de controle para curva Bezier cúbica (mais orgânica)
        var midX1 = (currentX + targetX) / 3 + Random.Shared.Next(-120, 121);
        var midY1 = (currentY + targetY) / 3 + Random.Shared.Next(-90, 91);
        var midX2 = 2 * (currentX + targetX) / 3 + Random.Shared.Next(-80, 81);
        var midY2 = 2 * (currentY + targetY) / 3 + Random.Shared.Next(-60, 61);

        // Número de passos: mais passos = mais suave e mais lento
        var distance = Math.Sqrt(Math.Pow(targetX - currentX, 2) + Math.Pow(targetY - currentY, 2));
        var steps = Math.Max(15, (int)(distance / 5)); // Mais passos que antes

        var prevX = currentX;
        var prevY = currentY;

        for (int i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double t = (double)i / steps;
            double oneMinusT = 1 - t;

            // Bezier cúbico: P = (1-t)³·P0 + 3·(1-t)²·t·P1 + 3·(1-t)·t²·P2 + t³·P3
            var bezierX = oneMinusT * oneMinusT * oneMinusT * currentX
                       + 3 * oneMinusT * oneMinusT * t * midX1
                       + 3 * oneMinusT * t * t * midX2
                       + t * t * t * targetX;
            var bezierY = oneMinusT * oneMinusT * oneMinusT * currentY
                       + 3 * oneMinusT * oneMinusT * t * midY1
                       + 3 * oneMinusT * t * t * midY2
                       + t * t * t * targetY;

            // Micro-tremor humano (mais forte no início, suave no fim)
            var jitter = (1 - t) * 4 + 0.3;
            var moveX = bezierX + (Random.Shared.NextDouble() - 0.5) * jitter * 2;
            var moveY = bezierY + (Random.Shared.NextDouble() - 0.5) * jitter * 2;

            var intX = (int)Math.Round(moveX);
            var intY = (int)Math.Round(moveY);

            if (intX != prevX || intY != prevY)
            {
                await page.Mouse.MoveAsync(intX, intY);
                prevX = intX;
                prevY = intY;
            }

            // Delay com distribuição natural (aceleração no início, desaceleração no fim)
            var baseDelay = LogNormalDelay(18, 10);
            double speedFactor;
            if (t < 0.15) speedFactor = 2.5;  // Início bem lento (acelerando)
            else if (t < 0.3) speedFactor = 1.8;
            else if (t < 0.7) speedFactor = 1.0;  // Meio mais rápido
            else if (t < 0.9) speedFactor = 1.6;  // Desacelerando
            else speedFactor = 2.2;  // Fim bem lento (precisão)

            await Task.Delay((int)(baseDelay * speedFactor), cancellationToken);
        }

        // Garantir que o mouse está exatamente no alvo
        await page.Mouse.MoveAsync(targetX, targetY);

        // 4. Hesitação realista antes do clique (pessoa pensa: "é aqui mesmo?")
        var hesitation = LogNormalDelay(280, 150);
        await Task.Delay(hesitation, cancellationToken);

        // 5. Clique com pressão variável (mousedown + delay + mouseup)
        await page.Mouse.DownAsync();
        var pressDuration = LogNormalDelay(85, 35);
        await Task.Delay(pressDuration, cancellationToken);
        await page.Mouse.UpAsync();

        _logger.LogDebug("Clique em ({X},{Y}) - hesitação: {Hes}ms, pressão: {Press}ms",
            targetX, targetY, hesitation, pressDuration);

        // 6. Pós-clique: humano processa o que clicou
        var postClickDelay = LogNormalDelay(300, 180);
        await Task.Delay(postClickDelay, cancellationToken);

        // 7. Ocasionalmente, scroll curto depois do clique (como se fosse ver o resultado)
        if (Random.Shared.NextDouble() < 0.25)
        {
            var scrollAmount = Random.Shared.Next(30, 120);
            await page.EvaluateAsync($"window.scrollBy(0, {scrollAmount})");
            await Task.Delay(LogNormalDelay(200, 100), cancellationToken);
        }
    }

    /// <summary>
    /// Clique rápido para células de CAPTCHA: movimento direto com poucos passos,
    /// hesitação e pós-clique mínimos. O desafio expira em ~30s, então cada clique
    /// deve levar o mínimo de tempo possível mantendo um leve traço humano.
    /// </summary>
    private async Task FastClickAsync(IPage page, int targetX, int targetY, CancellationToken cancellationToken)
    {
        // Posição inicial próxima do alvo (menos movimento = mais rápido)
        var currentX = Math.Clamp(targetX + Random.Shared.Next(-80, 81), 0, 1920);
        var currentY = Math.Clamp(targetY + Random.Shared.Next(-60, 61), 0, 1080);

        // Poucos passos, movimento direto com leve arco humano
        var steps = Random.Shared.Next(8, 16);
        var prevX = currentX;
        var prevY = currentY;

        for (int i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double t = (double)i / steps;
            var intX = (int)Math.Round(currentX + (targetX - currentX) * t);
            var intY = (int)Math.Round(currentY + (targetY - currentY) * t
                + Math.Sin(t * Math.PI) * Random.Shared.Next(-4, 5));

            if (intX != prevX || intY != prevY)
            {
                await page.Mouse.MoveAsync(intX, intY);
                prevX = intX;
                prevY = intY;
            }

            await Task.Delay(Random.Shared.Next(8, 18), cancellationToken);
        }

        await page.Mouse.MoveAsync(targetX, targetY);

        // Hesitação curta
        await Task.Delay(Random.Shared.Next(40, 100), cancellationToken);

        // Clique
        await page.Mouse.DownAsync();
        await Task.Delay(Random.Shared.Next(40, 80), cancellationToken);
        await page.Mouse.UpAsync();

        // Pós-clique curto
        await Task.Delay(Random.Shared.Next(60, 140), cancellationToken);
    }

    public async Task ClickElementHumanizedAsync(IPage page, IElementHandle element, CancellationToken cancellationToken = default)
    {
        var box = await element.BoundingBoxAsync();
        if (box == null)
        {
            _logger.LogWarning("Não foi possível obter bounding box do elemento");
            return;
        }

        // Clicar em um ponto aleatório DENTRO do elemento (nunca exatamente no centro)
        var offsetXFrac = 0.15 + Random.Shared.NextDouble() * 0.5; // 15%-65% da largura
        var offsetYFrac = 0.15 + Random.Shared.NextDouble() * 0.5; // 15%-65% da altura
        var targetX = (int)(box.X + box.Width * offsetXFrac);
        var targetY = (int)(box.Y + box.Height * offsetYFrac);

        await ClickHumanizedAsync(page, targetX, targetY, cancellationToken);
    }

    public async Task ClickAroundAsync(IPage page, int centerX, int centerY, int radius = 50, int attempts = 5, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < attempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Posição aleatória com distribuição mais natural (mais perto do centro)
            var angle = Random.Shared.NextDouble() * 2 * Math.PI;
            // Usar distribuição quadrática para concentrar mais perto do centro
            var distance = (int)(radius * Math.Sqrt(Random.Shared.NextDouble()));

            var offsetX = (int)(Math.Cos(angle) * distance);
            var offsetY = (int)(Math.Sin(angle) * distance);

            var targetX = centerX + offsetX;
            var targetY = centerY + offsetY;

            _logger.LogDebug("Clique exploratório {Attempt}/{Max} em ({X},{Y})",
                i + 1, attempts, targetX, targetY);

            await ClickHumanizedAsync(page, targetX, targetY, cancellationToken);

            // Delay orgânico entre tentativas
            var betweenDelay = LogNormalDelay(1200, 600);
            await Task.Delay(betweenDelay, cancellationToken);
        }
    }

    public async Task SimulateThinkingAsync(IPage page, CancellationToken cancellationToken = default)
    {
        // Pausa natural: pessoa "lê" a tela ou pensa no que fazer
        var thinkTime = LogNormalDelay(1400, 700);
        _logger.LogDebug("Simulando pensamento por {Delay}ms...", thinkTime);

        // Pequenos micro-movimentos durante a pausa (como se estivesse lendo)
        var halfTime = thinkTime / 2;
        await Task.Delay(halfTime, cancellationToken);

        if (Random.Shared.NextDouble() < 0.4)
        {
            // Mexer o mouse levemente enquanto "pensa"
            var smallMoveX = Random.Shared.Next(-15, 16);
            var smallMoveY = Random.Shared.Next(-10, 11);
            await page.Mouse.MoveAsync(500 + smallMoveX, 400 + smallMoveY);
        }

        await Task.Delay(thinkTime - halfTime, cancellationToken);
    }

    public async Task SimulateScrollAsync(IPage page, int deltaY, CancellationToken cancellationToken = default)
    {
        // Scroll lento e gradual (não instantâneo)
        var steps = Math.Max(3, Math.Abs(deltaY) / 30);
        var stepSize = deltaY / steps;

        for (int i = 0; i < steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.EvaluateAsync($"window.scrollBy(0, {stepSize})");
            var scrollDelay = LogNormalDelay(40, 20);
            await Task.Delay(scrollDelay, cancellationToken);
        }
    }
}
