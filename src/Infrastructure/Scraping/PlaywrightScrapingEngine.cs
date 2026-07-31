using Domain.Entities;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Scraping;

/// <summary>
/// Motor de scraping real utilizando Playwright para extração do Google Search.
/// Técnicas stealth: rotação de User-Agent, remoção de webdriver, delays humanos, detecção de CAPTCHA.
/// </summary>
public class PlaywrightScrapingEngine : IScrapingEngine
{
    private readonly IUserAgentRotationService _userAgentService;
    private readonly ICaptchaDetectionService _captchaDetection;
    private readonly IHumanClickService _humanClick;
    private readonly IImageClassifier _classifier;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PlaywrightScrapingEngine> _logger;
    private readonly int _maxRetries;
    private readonly int _humanDelayMinMs;
    private readonly int _humanDelayMaxMs;
    private readonly int _captchaTimeoutMs;
    private readonly int _maxCaptchaRounds;
    private readonly int _maxPages;

    private const string NextPageSelector = "a#pnnext, a[aria-label*='Próxima'], a[aria-label*='Next'], a[rel='next']";

    private const string StealthScript = @"
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
        window.chrome = { runtime: {} };
        Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
        Object.defineProperty(navigator, 'languages', { get: () => ['pt-BR', 'pt', 'en-US', 'en'] });
    ";

    public PlaywrightScrapingEngine(
        IUserAgentRotationService userAgentService,
        ICaptchaDetectionService captchaDetection,
        IHumanClickService humanClick,
        IImageClassifier classifier,
        IHttpClientFactory httpClientFactory,
        ILogger<PlaywrightScrapingEngine> logger,
        IConfiguration configuration)
    {
        _userAgentService = userAgentService;
        _captchaDetection = captchaDetection;
        _humanClick = humanClick;
        _classifier = classifier;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var scrapingSection = configuration.GetSection("ScrapingSettings");
        _maxRetries = scrapingSection.GetValue("MaxRetries", 3);
        _humanDelayMinMs = scrapingSection.GetValue("HumanDelayMinMs", 500);
        _humanDelayMaxMs = scrapingSection.GetValue("HumanDelayMaxMs", 2000);
        _captchaTimeoutMs = scrapingSection.GetValue("CaptchaTimeoutMs", 20000);
        _maxCaptchaRounds = scrapingSection.GetValue("MaxCaptchaRounds", 3);
        _maxPages = scrapingSection.GetValue("MaxPages", 10);
    }

    public async Task<ScrapingResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default)
    {
        var searchList = job.SearchList
            ?? throw new InvalidOperationException($"Job {job.Id} não tem SearchList associada");

        var results = new List<SearchResultData>();
        string? lastError = null;
        var totalKeywords = searchList.Keywords.Count;

        _logger.LogInformation(
            "Iniciando scraping para lista '{ListName}' ({KeywordCount} keywords, {DomainCount} domínios)",
            searchList.Name, totalKeywords, searchList.Domains.Count);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--window-size=1920,1080"
            }
        });

        for (int kwIndex = 0; kwIndex < totalKeywords; kwIndex++)
        {
            var keyword = searchList.Keywords[kwIndex];
            var retryCount = 0;
            var success = false;

            _logger.LogInformation(
                "Processando keyword [{Index}/{Total}]: '{Keyword}'",
                kwIndex + 1, totalKeywords, keyword);

            while (retryCount < _maxRetries && !success)
            {
                try
                {
                    var keywordResults = await ScrapeKeywordAsync(
                        browser, keyword, searchList.Domains, cancellationToken);

                    results.AddRange(keywordResults);
                    success = true;

                    _logger.LogInformation(
                        "Keyword '{Keyword}': {ResultCount} posições encontradas",
                        keyword, keywordResults.Count);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    retryCount++;
                    lastError = ex.Message;
                    _logger.LogWarning(ex,
                        "Erro ao processar keyword '{Keyword}' (tentativa {Retry}/{MaxRetries})",
                        keyword, retryCount, _maxRetries);

                    if (retryCount < _maxRetries)
                    {
                        var retryDelay = Random.Shared.Next(3000, 8000);
                        await Task.Delay(retryDelay, cancellationToken);
                    }
                }
            }

            if (!success)
            {
                return new ScrapingResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Falha após {_maxRetries} retries na keyword '{keyword}': {lastError}",
                    Data = results
                };
            }

            if (kwIndex < totalKeywords - 1)
            {
                var interKeywordDelay = Random.Shared.Next(_humanDelayMinMs, _humanDelayMaxMs) * 2;
                await Task.Delay(interKeywordDelay, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Scraping concluído para '{ListName}': {TotalResults} posições encontradas",
            searchList.Name, results.Count);

        return new ScrapingResult { IsSuccess = true, Data = results };
    }

    private async Task<List<SearchResultData>> ScrapeKeywordAsync(
        IBrowser browser,
        string keyword,
        List<string> targetDomains,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultData>();
        var userAgent = _userAgentService.GetRandomUserAgent();

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = userAgent,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "pt-BR",
            TimezoneId = "America/Sao_Paulo",
            ColorScheme = ColorScheme.Light
        });

        await context.AddInitScriptAsync(StealthScript);
        var page = await context.NewPageAsync();

        try
        {
            await page.Mouse.MoveAsync(Random.Shared.Next(100, 500), Random.Shared.Next(100, 500));

            var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(keyword)}&hl=pt-BR&gl=br";
            await page.GotoAsync(searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            var readingDelay = Random.Shared.Next(_humanDelayMinMs, _humanDelayMaxMs);
            await Task.Delay(readingDelay, cancellationToken);

            // Aguardar pagina estabilizar completamente
            try
            {
                await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 10000 });
            }
            catch { /* Timeout e OK - pagina pode ter redirecionado */ }

            // Verificar CAPTCHA na primeira página
            await HandleCaptchaIfPresentAsync(page, keyword, cancellationToken);

            // Coletar primeira página
            var pageNumber = 1;
            var currentStart = 0;
            var pageResults = await ExtractResultsFromPageAsync(page, keyword, targetDomains, currentStart, cancellationToken);
            results.AddRange(pageResults);
            _logger.LogInformation("Pagina {Page}: {Count} resultados coletados (total {Total})",
                pageNumber, pageResults.Count, results.Count);

            // Paginação: navegar e coletar até a última página ou atingir o limite máximo.
            // Google usa 10 resultados por página (start = 10, 20, 30...).
            while (pageNumber < _maxPages)
            {
                // Se não há mais botão "Próxima", terminar a paginação
                if (!await HasNextPageAsync(page))
                {
                    _logger.LogInformation("Nenhuma proxima pagina encontrada. Fim da paginacao na pagina {Page}.", pageNumber);
                    break;
                }

                currentStart += 10;
                pageNumber++;

                _logger.LogInformation("Navegando para a pagina {Page} (start={Start})...", pageNumber, currentStart);

                var navigated = await NavigateToNextPageAsync(page, searchUrl, currentStart, cancellationToken);
                if (!navigated)
                {
                    _logger.LogWarning("Falha ao navegar para a pagina {Page}. Fim da paginacao.", pageNumber);
                    break;
                }

                // SEMPRE verificar se há CAPTCHA antes de coletar a próxima página.
                // Se aparecer CAPTCHA e não for possível resolver, paramos a paginação
                // mas mantemos os resultados já coletados nas páginas anteriores.
                try
                {
                    await HandleCaptchaIfPresentAsync(page, keyword, cancellationToken);
                }
                catch (OperationCanceledException) { throw; } // propagar cancelamento do usuario (botao Parar)
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CAPTCHA na pagina {Page} nao resolvido. Parando paginacao com {Total} resultados coletados.",
                        pageNumber, results.Count);
                    break;
                }

                pageResults = await ExtractResultsFromPageAsync(page, keyword, targetDomains, currentStart, cancellationToken);
                if (pageResults.Count == 0)
                {
                    _logger.LogInformation("Pagina {Page} sem resultados. Fim da paginacao.", pageNumber);
                    break;
                }

                results.AddRange(pageResults);
                _logger.LogInformation("Pagina {Page}: {Count} resultados coletados (total {Total})",
                    pageNumber, pageResults.Count, results.Count);

                // Delay humanizado entre páginas
                var betweenPagesDelay = Random.Shared.Next(2000, 4000);
                await Task.Delay(betweenPagesDelay, cancellationToken);
            }

            _logger.LogInformation("Scraping da keyword '{Keyword}' concluido: {Total} resultados em {Pages} paginas",
                keyword, results.Count, pageNumber);

            await page.EvaluateAsync("window.scrollBy(0, 300)");
            await Task.Delay(Random.Shared.Next(500, 1500), cancellationToken);
        }
        finally
        {
            await context.CloseAsync();
        }

        return results;
    }

    /// <summary>
    /// Verifica se há CAPTCHA na página atual e tenta resolver com cliques humanizados.
    /// Se o CAPTCHA for resolvido, aguarda o redirecionamento do /sorry/ para a página
    /// de resultados e a carga dos resultados (div.g).
    /// Se NÃO houver CAPTCHA, retorna imediatamente.
    /// Lança InvalidOperationException se o CAPTCHA não puder ser resolvido.
    /// </summary>
    private async Task HandleCaptchaIfPresentAsync(IPage page, string keyword, CancellationToken cancellationToken)
    {
        if (!await _captchaDetection.DetectAsync(page, keyword, cancellationToken))
            return;

        // Tentar resolver CAPTCHA com cliques humanizados
        _logger.LogInformation("Tentando resolver CAPTCHA com cliques humanizados...");
        var solved = await TrySolveCaptchaAsync(page, keyword, cancellationToken);

        if (!solved)
        {
            throw new InvalidOperationException(
                "CAPTCHA/verificação de segurança detectada pelo Google.");
        }

        _logger.LogInformation("CAPTCHA possivelmente resolvido, continuando...");

        // Após resolver o CAPTCHA, o Google redireciona do /sorry/
        // para a página de resultados (parâmetro 'continue').
        // PRECISAMOS aguardar esse redirect — se verificarmos antes,
        // a URL ainda contém /sorry/ e o DetectAsync acusa falso
        // positivo, abortando antes de coletar os dados.
        var redirectDeadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < redirectDeadline &&
               page.Url.Contains("/sorry/", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(500, cancellationToken);
        }

        if (page.Url.Contains("/sorry/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Redirecionamento do /sorry/ nao concluido em 15s — continuando mesmo assim");
        }
        else
        {
            _logger.LogInformation("Redirecionado para pagina de resultados: {Url}", page.Url);
        }

        // Aguardar os resultados de busca carregarem (div.g)
        try
        {
            await page.WaitForSelectorAsync("div.g", new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        catch { /* pode nao ter resultados (pagina legitima sem resultados) */ }

        // Verificar novamente se passou
        if (await _captchaDetection.DetectAsync(page, keyword, cancellationToken))
        {
            throw new InvalidOperationException(
                "CAPTCHA/verificação de segurança detectada pelo Google.");
        }
    }

    /// <summary>
    /// Extrai os resultados de busca da página atual.
    /// O Google envolve links externos como /url?q=https://...&sa=U&... —
    /// extraímos a URL real do parâmetro 'q'. A posição é calculada considerando
    /// o offset (start) para que cada página tenha posições contínuas.
    /// </summary>
    private async Task<List<SearchResultData>> ExtractResultsFromPageAsync(
        IPage page,
        string keyword,
        List<string> targetDomains,
        int positionOffset,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultData>();

        // Aguardar os resultados carregarem antes de extrair
        try
        {
            await page.WaitForSelectorAsync("div.g, div.tF2Cxc", new PageWaitForSelectorOptions { Timeout = 10000 });
        }
        catch { /* pode nao ter resultados */ }

        // Google pode variar a estrutura do DOM (div.g classico ou div.tF2Cxc novo)
        var searchResultElements = await page.QuerySelectorAllAsync("div.g, div.tF2Cxc, div[data-sncf]");
        _logger.LogInformation("Elementos de resultado encontrados: {Count}", searchResultElements.Count);

        for (int i = 0; i < searchResultElements.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = searchResultElements[i];
            try
            {
                var linkElement = await element.QuerySelectorAsync("a[href]");
                if (linkElement == null) continue;

                var href = await linkElement.GetAttributeAsync("href");
                if (string.IsNullOrEmpty(href)) continue;

                // Google envolve links externos como /url?q=https://...&sa=U&...
                // Precisamos extrair a URL real do parametro 'q'.
                if (href.StartsWith("/url?q=", StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("http://www.google.com/url?q=", StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("https://www.google.com/url?q=", StringComparison.OrdinalIgnoreCase))
                {
                    var qIndex = href.IndexOf("q=", StringComparison.Ordinal);
                    if (qIndex >= 0)
                    {
                        var qValue = href[(qIndex + 2)..];
                        var ampIndex = qValue.IndexOf('&');
                        if (ampIndex >= 0)
                            qValue = qValue[..ampIndex];

                        try { href = Uri.UnescapeDataString(qValue); }
                        catch { /* manter original se falhar */ }
                    }
                }

                if (!href.StartsWith("http")) continue;

                var uri = new Uri(href);
                var domain = uri.Host.Replace("www.", "");

                var titleElement = await element.QuerySelectorAsync("h3");
                var title = titleElement != null
                    ? await titleElement.TextContentAsync() ?? string.Empty
                    : string.Empty;

                var snippetElement = await element.QuerySelectorAsync("[data-sncf], .VwiC3b, .s3v9rd");
                var snippet = snippetElement != null
                    ? await snippetElement.TextContentAsync() ?? string.Empty
                    : string.Empty;

                if (!targetDomains.Any() || targetDomains.Any(d =>
                    domain.Contains(d, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new SearchResultData
                    {
                        Keyword = keyword,
                        Domain = domain,
                        Position = positionOffset + i + 1,
                        Url = href,
                        Title = title.Trim(),
                        Snippet = snippet.Trim()
                    });
                    _logger.LogInformation("Resultado #{Index}: {Domain} - {Title}", positionOffset + i + 1, domain, title.Trim());
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao extrair resultado #{Index}", i + 1);
            }
        }

        return results;
    }

    /// <summary>
    /// Verifica se existe um link para a próxima página de resultados do Google.
    /// </summary>
    private async Task<bool> HasNextPageAsync(IPage page)
    {
        try
        {
            var nextLink = await page.QuerySelectorAsync(NextPageSelector);
            return nextLink != null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao verificar proxima pagina");
            return false;
        }
    }

    /// <summary>
    /// Navega para a próxima página de resultados com comportamento humanizado:
    /// scroll lento até o rodapé onde fica a paginação, pausa de "pensamento"
    /// e clique no link "Próxima página". Se o link não existir (layout novo),
    /// faz fallback para navegação direta por URL com o parâmetro start.
    /// </summary>
    private async Task<bool> NavigateToNextPageAsync(
        IPage page,
        string baseSearchUrl,
        int nextStart,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Scroll lento e natural até a área de paginação (rodapé)
            var scrollDistance = Random.Shared.Next(1200, 2500);
            await _humanClick.SimulateScrollAsync(page, scrollDistance, cancellationToken);

            // 2. Pausa para "pensar" antes de clicar
            await _humanClick.SimulateThinkingAsync(page, cancellationToken);

            // 3. Encontrar o link da próxima página
            var nextLink = await page.QuerySelectorAsync(NextPageSelector);

            if (nextLink != null)
            {
                // Rolagem até o link para garantir visibilidade
                try
                {
                    await nextLink.ScrollIntoViewIfNeededAsync();
                }
                catch { /* elemento pode ja estar visivel */ }

                await _humanClick.SimulateThinkingAsync(page, cancellationToken);
                await _humanClick.ClickElementHumanizedAsync(page, nextLink, cancellationToken);

                _logger.LogInformation("Clique humanizado no link 'Proxima pagina' executado");
            }

            // 4. Aguardar a nova página carregar e estabilizar
            try
            {
                await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15000 });
            }
            catch { /* Timeout e OK - pode estar em transicao */ }

            var settleDelay = Random.Shared.Next(1500, 3000);
            await Task.Delay(settleDelay, cancellationToken);

            // 5. SEMPRE verificar que a pagina realmente mudou. Se o clique falhou
            // silenciosamente (overlay, elemento movido, link desanexado), a URL
            // nao contem o start esperado e o DOM seria re-lido (duplicando
            // resultados com posicoes erradas). Nesse caso, navegar por URL.
            if (!page.Url.Contains($"start={nextStart}", StringComparison.OrdinalIgnoreCase))
            {
                var nextUrl = $"{baseSearchUrl}&start={nextStart}";
                _logger.LogWarning("URL nao mudou apos navegacao (start={Start}). Navegando por URL: {Url}", nextStart, nextUrl);

                await page.GotoAsync(nextUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30000
                });

                try
                {
                    await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15000 });
                }
                catch { /* Timeout e OK */ }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao navegar para proxima pagina");
            return false;
        }
    }

    /// <summary>
    /// Tenta resolver CAPTCHA com timeout de {_captchaTimeoutMs}ms e comportamento humanizado.
    /// Se o desafio expirar ("expirou" / "marque a caixa"), detecta e tenta novamente.
    /// </summary>
    private async Task<bool> TrySolveCaptchaAsync(IPage page, string keyword, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // CADA tentativa tem seu próprio timeout de {_captchaTimeoutMs}ms.
            // Antes o timeout era compartilhado entre as tentativas, fazendo
            // a tentativa 2 falhar sempre com timeout (restavam poucos ms).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_captchaTimeoutMs);

            try
            {
                _logger.LogInformation("Tentativa {Attempt}/{MaxAttempts} - URL: {Url}", attempt, maxAttempts, page.Url);

                // Pausa natural antes de comecar (apenas na segunda tentativa, se houve expiracao)
                if (attempt > 1)
                    await _humanClick.SimulateThinkingAsync(page, timeoutCts.Token);

                // Estrategia 1: Clicar no checkbox reCAPTCHA dentro do iframe
                var recaptchaIframe = await page.QuerySelectorAsync("iframe[src*='recaptcha'], iframe[src*='anchor']");
                if (recaptchaIframe != null)
                {
                    _logger.LogInformation("Iframe reCAPTCHA encontrado, acessando conteudo...");
                    var frame = await recaptchaIframe.ContentFrameAsync();
                    if (frame != null)
                    {
                        var checkbox = await frame.QuerySelectorAsync("#recaptcha-anchor, .recaptcha-checkbox-border, [role='checkbox']");
                        if (checkbox != null)
                        {
                            _logger.LogInformation("Checkbox encontrado, movendo mouse...");

                            if (attempt == 1)
                                await _humanClick.SimulateThinkingAsync(page, timeoutCts.Token);

                            var box = await checkbox.BoundingBoxAsync();
                            if (box != null)
                            {
                                var clickX = (int)(box.X + box.Width * (0.25 + Random.Shared.NextDouble() * 0.5));
                                var clickY = (int)(box.Y + box.Height * (0.25 + Random.Shared.NextDouble() * 0.5));
                                await _humanClick.ClickHumanizedAsync(page, clickX, clickY, timeoutCts.Token);
                            }
                            else
                            {
                                await _humanClick.ClickElementHumanizedAsync(page, checkbox, timeoutCts.Token);
                            }

                            // Aguardar resposta do Google (3-6s)
                            _logger.LogInformation("Aguardando resposta do reCAPTCHA...");
                            var waitAfterCheckbox = Random.Shared.Next(3000, 6000);
                            await Task.Delay(waitAfterCheckbox, timeoutCts.Token);
                            _logger.LogInformation("Clique no checkbox concluido");

                            // Pausa curta para "ver" o resultado
                            await _humanClick.SimulateThinkingAsync(page, timeoutCts.Token);

                            if (await HasImageGridChallengeAsync(page, timeoutCts.Token))
                            {
                                _logger.LogInformation("CAPTCHA de imagem detectado, tentando resolver...");

                                // SolveImageGridChallengeAsync usa cancellationToken (sem timeout de 20s)
                                // porque o HTTP do webhook tem timeout proprio. Se falhar,
                                // verificar expiracao e retentar com a pagina recarregada.
                                var solved = await SolveImageGridChallengeAsync(page, cancellationToken);
                                if (solved)
                                    return true;

                                _logger.LogWarning("SolveImageGridChallengeAsync retornou false na tentativa {Attempt}", attempt);

                                // Sempre tentar novamente (se houver tentativas restantes) —
                                // a expiracao ja foi detectada DENTRO de SolveImageGridFallbackAsync.
                                // Nao re-verificamos HasChallengeExpiredAsync aqui porque o estado
                                // da pagina pode ter mudado desde a deteccao dentro do round.
                                if (attempt < maxAttempts)
                                {
                                    _logger.LogInformation("SolveImageGridChallengeAsync retornou false. Reload e retentativa...");
                                    try { await page.ReloadAsync(new PageReloadOptions { Timeout = 15000 }); }
                                    catch { /* ignorar erro de reload */ }
                                    continue;
                                }

                                return false;
                            }

                            _logger.LogWarning("Grid de imagem NAO detectada apos clique no checkbox");
                            return true;
                        }
                    }
                }

                // Estrategia 2: Resolver CAPTCHA de imagem diretamente (sem checkbox)
                if (await HasImageGridChallengeAsync(page, timeoutCts.Token))
                {
                    _logger.LogInformation("CAPTCHA de imagem detectado diretamente, tentando resolver...");

                    var solved = await SolveImageGridChallengeAsync(page, cancellationToken);
                    if (solved)
                        return true;

                    _logger.LogWarning("SolveImageGridChallengeAsync retornou false na tentativa {Attempt} (estrategia 2)", attempt);

                    // Sempre tentar novamente (se houver tentativas restantes)
                    if (attempt < maxAttempts)
                    {
                        _logger.LogInformation("SolveImageGridChallengeAsync retornou false (estrategia 2). Reload e retentativa...");
                        try { await page.ReloadAsync(new PageReloadOptions { Timeout = 15000 }); }
                        catch { }
                        continue;
                    }

                    return false;
                }

                // Estrategia 3: Clicar no container div.g-recaptcha
                var recaptchaDiv = await page.QuerySelectorAsync("div.g-recaptcha");
                if (recaptchaDiv != null)
                {
                    _logger.LogInformation("Div g-recaptcha encontrado, clicando...");
                    await _humanClick.SimulateThinkingAsync(page, timeoutCts.Token);
                    await _humanClick.ClickElementHumanizedAsync(page, recaptchaDiv, timeoutCts.Token);
                    await Task.Delay(Random.Shared.Next(4000, 7000), timeoutCts.Token);
                    return true;
                }

                // Estrategia 4: Clicar no formulario de challenge
                var challengeForm = await page.QuerySelectorAsync("form[action*='challenge']");
                if (challengeForm != null)
                {
                    _logger.LogInformation("Challenge form encontrado, clicando...");
                    await _humanClick.SimulateThinkingAsync(page, timeoutCts.Token);
                    await _humanClick.ClickElementHumanizedAsync(page, challengeForm, timeoutCts.Token);
                    await Task.Delay(Random.Shared.Next(4000, 6000), timeoutCts.Token);
                    return true;
                }

                // Nenhum elemento CAPTCHA encontrado
                var viewportSize = page.ViewportSize;
                var centerX = (viewportSize?.Width ?? 1920) / 2;
                var centerY = (viewportSize?.Height ?? 1080) / 2;
                _logger.LogInformation("Nenhum elemento CAPTCHA encontrado, tentando cliques exploratorios...");
                await _humanClick.ClickAroundAsync(page, centerX, centerY, radius: 120, attempts: 5, timeoutCts.Token);
                return false;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout de 20s — verificar se foi expiracao do CAPTCHA e tentar novamente
                _logger.LogWarning("Timeout de {Timeout}ms na tentativa {Attempt}", _captchaTimeoutMs, attempt);

                if (attempt < maxAttempts && await HasChallengeExpiredAsync(page))
                {
                    _logger.LogInformation("Desafio expirou! Tentando novamente (tentativa {NextAttempt}/{MaxAttempts})...",
                        attempt + 1, maxAttempts);
                    continue;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na tentativa {Attempt} de resolver CAPTCHA", attempt);

                if (attempt < maxAttempts && await HasChallengeExpiredAsync(page))
                {
                    _logger.LogInformation("Desafio expirou apos erro! Tentando novamente...");
                    continue;
                }

                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Verifica rapidamente se ha um CAPTCHA de selecao de imagens (grid) na pagina.
    /// O bframe geralmente aparece em 1-3s apos clicar no checkbox — nao precisamos
    /// de 12 tentativas com delays longos (isso faz o desafio expirar).
    /// </summary>
    private async Task<bool> HasImageGridChallengeAsync(IPage page, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bframeIframe = await page.QuerySelectorAsync("iframe[src*='/bframe']");
            if (bframeIframe != null)
            {
                var frame = await bframeIframe.ContentFrameAsync();
                if (frame != null)
                {
                    var gridImage = await frame.QuerySelectorAsync(".rc-imageselect-target table, .rc-imageselect-target");
                    if (gridImage != null)
                    {
                        _logger.LogDebug("Grid de imagem detectada dentro do bframe");
                        return true;
                    }

                    var instruction = await frame.QuerySelectorAsync(".rc-imageselect-desc-no-canonical, .rc-imageselect-desc");
                    if (instruction != null)
                    {
                        _logger.LogDebug("Instrucao de CAPTCHA de imagem detectada");
                        return true;
                    }
                }
            }

            if (attempt < 4)
            {
                // Delay curto entre tentativas — o bframe carrega rapido
                var delay = Random.Shared.Next(300, 800);
                await Task.Delay(delay, cancellationToken);
            }
        }
        return false;
    }

    /// <summary>
    /// Verifica se o checkbox do reCAPTCHA reapareceu (desmarcado), o que indica
    /// que o desafio expirou. MAIS CONFIÁVEL que apenas checar texto na página,
    /// pois o checkbox pode aparecer antes da mensagem de expiração.
    /// </summary>
    private async Task<bool> HasCheckboxReappearedAsync(IPage page)
    {
        try
        {
            var recaptchaIframe = await page.QuerySelectorAsync("iframe[src*='recaptcha'], iframe[src*='anchor']");
            if (recaptchaIframe == null)
                return false;

            var frame = await recaptchaIframe.ContentFrameAsync();
            if (frame == null)
                return false;

            var checkbox = await frame.QuerySelectorAsync("#recaptcha-anchor, [role='checkbox']");
            if (checkbox == null)
                return false;

            var isChecked = await checkbox.GetAttributeAsync("aria-checked");
            // Se o checkbox existe e NAO esta marcado ("false"), o desafio expirou
            return string.Equals(isChecked, "false", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica se o CAPTCHA expirou através de texto de expiração na página.
    /// 
    /// NOTA: Apenas a verificação de TEXTO é usada (não o checkbox) porque
    /// o `HasCheckboxReappearedAsync` estava retornando FALSO POSITIVO:
    /// detectava o checkbox como "desmarcado" mesmo durante um desafio ativo,
    /// impedindo o fluxo de resolução de chegar ao n8n.
    /// 
    /// O text check é mais lento (~30s para detectar expiração real) mas
    /// NÃO causa falsos positivos.
    /// </summary>
    private async Task<bool> HasChallengeExpiredAsync(IPage page)
    {
        try
        {
            // Regex específico que exige contexto (ex: "desafio...expirou") em vez de
            // palavras soltas que podem aparecer em texto inocente da página.
            var matchedText = await page.EvaluateAsync<string?>(
                @"() => {
                    const t = document.body.innerText;
                    const patterns = [
                        /desafio.*expirou/i,
                        /verificação.*expirou/i,
                        /verificacao.*expirou/i,
                        /challenge.*expired/i,
                        /marque a caixa.*novamente/i,
                        /check the box.*again/i,
                        /captcha.*expirou/i,
                        /expirou.*tente novamente/i,
                        /expired.*try again/i
                    ];
                    for (const p of patterns) {
                        const match = t.match(p);
                        if (match) return match[0].substring(0, 120);
                    }
                    return null;
                }");
            if (matchedText != null)
            {
                _logger.LogWarning("HasChallengeExpiredAsync: TEXTO de expiração detectado na página: '{MatchedText}'", matchedText);
                return true;
            }
        }
        catch { /* ignorar */ }

        return false;
    }

    /// <summary>
    /// Resolve CAPTCHA de selecao de imagens.
    /// Captura UM screenshot do container inteiro (.rc-imageselect) e recorta
    /// header + celulas em memoria — sem "piscada" visivel no navegador.
    /// </summary>
    private async Task<bool> SolveImageGridChallengeAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Encontrar o iframe bframe (challenge de imagem)
            var bframeIframe = await page.QuerySelectorAsync("iframe[src*='/bframe']");
            if (bframeIframe == null)
            {
                _logger.LogWarning("Iframe bframe nao encontrado para CAPTCHA de imagem");
                return false;
            }

            var frame = await bframeIframe.ContentFrameAsync();
            if (frame == null)
            {
                _logger.LogWarning("Nao foi possivel acessar bframe");
                return false;
            }

            // 2. Extrair instrucao do header (ex: "Selecione todas as imagens com carros")
            var instructionElement = await frame.QuerySelectorAsync(".rc-imageselect-desc-no-canonical, .rc-imageselect-desc");
            string instruction = instructionElement != null
                ? await instructionElement.TextContentAsync() ?? ""
                : "";

            _logger.LogInformation("Instrucao do CAPTCHA: {Instruction}", instruction);

            // 3. Extrair palavras-chave da instrucao
            var keywords = ExtractKeywordsFromInstruction(instruction);
            if (keywords.Count == 0)
            {
                _logger.LogWarning("Nao foi possivel extrair palavras-chave da instrucao");
                return false;
            }

            _logger.LogInformation("Palavras-chave extraidas: {Keywords}", string.Join(", ", keywords));

            // 4. Enviar imagens para webhook n8n para classificacao
            return await SolveImageGridFallbackAsync(frame, page, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao resolver CAPTCHA de imagem");
            return false;
        }
    }




    // DTOs para comunicacao com o n8n webhook
    private class CaptchaWebhookRequest
    {
        [JsonPropertyName("header")]
        public string Header { get; set; } = "";

        [JsonPropertyName("grid")]
        public List<string> Grid { get; set; } = new();
    }

    private class CaptchaWebhookResponse
    {
        [JsonPropertyName("result")]
        public List<bool> Result { get; set; } = new();
    }

    private async Task<bool> SolveImageGridFallbackAsync(
        IFrame frame, IPage page, CancellationToken cancellationToken)
    {
        var cells = await frame.QuerySelectorAllAsync("td.rc-imageselect-tile");
        if (cells.Count == 0)
        {
            _logger.LogWarning("Nenhuma celula encontrada na grid");
            return false;
        }

        // Multi-round: lidar com rotacao de imagens ("Verifique tambem as novas imagens")
        // Nota: o timeout de 20s do TrySolveCaptchaAsync protege o fluxo total.
        // Dentro do round, aplicamos timeout APENAS na chamada HTTP do webhook
        // (12s) — o resto (cliques, thinking, verify) nao deve ser interrompido
        // prematuramente.
        var lastInstruction = "";
        for (int round = 1; round <= _maxCaptchaRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("=== ROUND {Round}/{MaxRounds} ===", round, _maxCaptchaRounds);

            // Re-obter celulas (podem ter mudado apos rotacionar imagens)
            cells = await frame.QuerySelectorAllAsync("td.rc-imageselect-tile");
            if (cells.Count == 0)
            {
                _logger.LogWarning("Nenhuma celula encontrada no round {Round}", round);
                return true; // Grid desapareceu = CAPTCHA resolvido
            }

            // Pausa natural antes de começar a examinar as imagens
            await _humanClick.SimulateThinkingAsync(page, cancellationToken);

            _logger.LogInformation("Capturando {Count} celulas (round {Round})...", cells.Count, round);

            // 1+2. Capturar header + grid em UM ÚNICO screenshot do container
            // (.rc-imageselect) e recortar header e células EM MEMÓRIA.
            //
            // ANTES: 1 screenshot POR CÉLULA (9-16 capturas por round) — o navegador
            // "piscava" a cada captura, um comportamento VISÍVEL e não humano que o
            // Google detecta e usa para banir. Agora é UMA captura discreta por round.
            string headerBase64 = "";
            var gridBase64List = new List<string>();
            try
            {
                // Fallback de containers: o layout do Google varia.
                // 1. .rc-imageselect        -> container padrao (header + grid)
                // 2. .rc-imageselect-target -> so a grid (usado em layouts novos)
                // 3. table                  -> ultimo recurso (as celulas td.rc-imageselect-tile
                //                                sempre estao dentro de uma table)
                // Qualquer um deles contem as celulas, entao o recorte por bounds funciona.
                var gridContainer = await frame.QuerySelectorAsync(".rc-imageselect")
                    ?? await frame.QuerySelectorAsync(".rc-imageselect-target table, .rc-imageselect-target")
                    ?? await frame.QuerySelectorAsync("table");

                if (gridContainer == null)
                {
                    _logger.LogWarning("Container da grid nao encontrado para screenshot unico");
                    return false;
                }

                var containerShot = await gridContainer.ScreenshotAsync();
                var containerBounds = await gridContainer.BoundingBoxAsync();

                if (containerBounds == null || containerShot == null || containerShot.Length == 0)
                {
                    _logger.LogWarning("Falha ao capturar container da grid (screenshot unico)");
                    return false;
                }

                using var containerMs = new MemoryStream(containerShot);
                using var containerBmp = new Bitmap(containerMs);

                // Escala CSS-pixels -> pixels da imagem (deviceScaleFactor)
                var scaleX = containerBmp.Width / (double)containerBounds.Width;
                var scaleY = containerBmp.Height / (double)containerBounds.Height;

                // Header (instrucao do CAPTCHA): screenshot SEPARADO (1 captura pequena
                // por round). O problema de "piscada" era causado pelas 9-16 capturas
                // individuais de celulas — uma captura do header e imperceptivel e evita
                // depender do header estar dentro do container (que varia por layout).
                var headerElement = await frame.QuerySelectorAsync(".rc-imageselect-desc-no-canonical, .rc-imageselect-desc");
                if (headerElement != null)
                {
                    var headerShot = await headerElement.ScreenshotAsync();
                    headerBase64 = Convert.ToBase64String(headerShot);
                }

                // Recortar cada celula da grid do MESMO screenshot
                for (int i = 0; i < cells.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cellBounds = await cells[i].BoundingBoxAsync();
                    if (cellBounds == null)
                    {
                        _logger.LogWarning("Bounds da celula {Index} nao disponiveis", i);
                        return false;
                    }

                    var x = (int)((cellBounds.X - containerBounds.X) * scaleX);
                    var y = (int)((cellBounds.Y - containerBounds.Y) * scaleY);
                    var w = (int)(cellBounds.Width * scaleX);
                    var h = (int)(cellBounds.Height * scaleY);
                    x = Math.Max(0, Math.Min(x, containerBmp.Width - 1));
                    y = Math.Max(0, Math.Min(y, containerBmp.Height - 1));
                    w = Math.Max(1, Math.Min(w, containerBmp.Width - x));
                    h = Math.Max(1, Math.Min(h, containerBmp.Height - y));

                    using var cellCrop = containerBmp.Clone(
                        new Rectangle(x, y, w, h), containerBmp.PixelFormat);
                    using var cellMs = new MemoryStream();
                    cellCrop.Save(cellMs, ImageFormat.Png);
                    gridBase64List.Add(Convert.ToBase64String(cellMs.ToArray()));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao capturar grid em screenshot unico");
                _logger.LogWarning("Falha ao capturar grid em screenshot unico, abortando...");
                return false;
            }

            _logger.LogInformation("Enviando {CellCount} celulas + header para webhook n8n (round {Round})...", gridBase64List.Count, round);

            // 3. Chamar webhook n8n
            var matchingBounds = new List<Microsoft.Playwright.ElementHandleBoundingBoxResult>();

            try
            {
                var webhookRequest = new CaptchaWebhookRequest
                {
                    Header = headerBase64,
                    Grid = gridBase64List
                };

                // Timeout de 12s APENAS para a chamada HTTP do webhook —
                // se o n8n demorar, o round é abortado (o CAPTCHA expiraria
                // durante a espera).
                var client = _httpClientFactory.CreateClient("CaptchaWebhook");
                using var webhookCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                webhookCts.CancelAfter(TimeSpan.FromSeconds(12));
                var response = await client.PostAsJsonAsync("", webhookRequest, webhookCts.Token);
                response.EnsureSuccessStatusCode();

                var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    _logger.LogWarning("WEBHOOK retornou resposta VAZIA no round {Round} - abortando (n8n indisponivel)", round);
                    return false;
                }

                // Logar a resposta raw (truncada, pois pode conter base64 de imagens)
                var truncatedJson = rawResponse.Length > 300 ? rawResponse[..300] + "... (" + rawResponse.Length + " chars total)" : rawResponse;
                _logger.LogInformation("WEBHOOK RESPONSE RAW (round {Round}): {RawJson}", round, truncatedJson);

                // Parse manual: ler array de bools do JSON bruto
                CaptchaWebhookResponse? webhookResponse = null;
                try
                {
                    using var doc = JsonDocument.Parse(rawResponse);
                    var root = doc.RootElement;

                    JsonElement resultElement = default;
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, "result", StringComparison.OrdinalIgnoreCase))
                        {
                            resultElement = prop.Value;
                            break;
                        }
                    }

                    if (resultElement.ValueKind == JsonValueKind.Array)
                    {
                        var boolList = new List<bool>();
                        foreach (var item in resultElement.EnumerateArray())
                        {
                            boolList.Add(item.GetBoolean());
                        }
                        webhookResponse = new CaptchaWebhookResponse { Result = boolList };
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogError(parseEx, "Falha ao parsear JSON do webhook no round {Round}: {RawJson}", round, rawResponse);
                }

                if (webhookResponse?.Result == null || webhookResponse.Result.Count != cells.Count)
                {
                    _logger.LogWarning("Resposta do webhook invalida: {Count} resultados para {CellCount} celulas",
                        webhookResponse?.Result?.Count ?? 0, cells.Count);
                    return false;
                }

                // Log completo: mostrar o array de bools e quais celulas estao selecionadas
                var resultSummary = string.Join(", ", webhookResponse.Result.Select((v, idx) => $"{idx}={(v ? "S" : "N")}"));
                _logger.LogInformation("Webhook resultado ({Count} celulas): [{Result}]", webhookResponse.Result.Count, resultSummary);                    // Verificar expiracao apos webhook — o webhook levou 3-5s,
                // o CAPTCHA pode ter expirado durante a espera
                if (await HasChallengeExpiredAsync(page))
                {
                    _logger.LogWarning("CAPTCHA expirou APOS webhook no round {Round}. Abortando para retentativa.", round);
                    return false;
                }

                // 4. Coletar bounds das celulas onde result[i] == true
                for (int i = 0; i < webhookResponse.Result.Count; i++)
                {
                    if (webhookResponse.Result[i])
                    {
                        var cellBounds = await cells[i].BoundingBoxAsync();
                        if (cellBounds != null)
                        {
                            matchingBounds.Add(cellBounds);
                            _logger.LogInformation("MATCH: Celula [{Index}] selecionada (bounds: X={X}, Y={Y})", i, cellBounds.X, cellBounds.Y);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("SKIP: Celula [{Index}] NAO selecionada", i);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro ao chamar webhook n8n para CAPTCHA");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout ao chamar webhook n8n para CAPTCHA");
                return false;
            }

            if (matchingBounds.Count == 0)
            {
                _logger.LogInformation("Nenhuma celula selecionada no round {Round} — clicando verify para finalizar", round);
                await ClickVerifyButton(frame, page, cancellationToken);
                return true;
            }

            _logger.LogInformation("Clicando em {Count} celulas (round {Round})...", matchingBounds.Count, round);

            // 5. Log detalhado das coordenadas de cada celula para debug
            for (int di = 0; di < matchingBounds.Count; di++)
            {
                var b = matchingBounds[di];
                _logger.LogInformation("Celula #{Index} bounds: X={X}, Y={Y}, W={W}, H={H}",
                    di, b.X, b.Y, b.Width, b.Height);
            }

            // Clicar nas celulas com comportamento humanizado
            // Importante: usar ClickAsync direto no elemento dentro do iframe
            // em vez de calcular coordenadas viewport manualmente.
            // O Playwright resolve automaticamente a posição do elemento
            // dentro do iframe, evitando erros de coordenadas.
            var dotIndex = 0;
            for (int ci = 0; ci < matchingBounds.Count; ci++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bounds = matchingBounds[ci];
                var randXFrac = 0.2 + Random.Shared.NextDouble() * 0.6;
                var randYFrac = 0.2 + Random.Shared.NextDouble() * 0.6;
                var clickX = (int)(bounds.X + bounds.Width * randXFrac);
                var clickY = (int)(bounds.Y + bounds.Height * randYFrac);

                _logger.LogInformation("Round {Round} - Clique #{Index}: celula bounds=({BoundsX},{BoundsY} {W}x{H}), click em ({ClickX},{ClickY})",
                    round, dotIndex + 1, bounds.X, bounds.Y, bounds.Width, bounds.Height, clickX, clickY);

                // VERIFICAR ANTES DE CADA CLIQUE se o desafio ainda está ativo.
                // IMPORTANTE: consultar a GRID primeiro — se as células ainda existem,
                // o desafio está ativo e clicamos normalmente (o checkbox pode
                // reportar "desmarcado" durante um desafio ativo, causando falso
                // positivo — por isso NÃO o consultamos enquanto a grid existe).
                var remainingCells = await frame.QuerySelectorAllAsync("td.rc-imageselect-tile");
                if (remainingCells.Count == 0)
                {
                    // Grid sumiu: distinguir resolvido (checkbox não reapareceu)
                    // de expirado (checkbox reapareceu desmarcado).
                    // NOTA: o checkbox só é consultado quando a grid sumiu,
                    // pois durante um desafio ativo ele pode reportar
                    // "desmarcado" (falso positivo conhecido).
                    if (await HasCheckboxReappearedAsync(page))
                    {
                        _logger.LogWarning("CAPTCHA expirou ANTES do clique #{Click} no round {Round} (checkbox reapareceu). Abortando para retentativa.",
                            dotIndex + 1, round);
                        return false;
                    }

                    _logger.LogInformation("Grid desapareceu ANTES do clique #{Click} no round {Round} - CAPTCHA resolvido!",
                        dotIndex + 1, round);
                    return true;
                }

                // Pausa CURTA para "pensar" antes de clicar — o desafio expira
                // em ~30s, então cada segundo conta. Antes era 600-2200ms.
                var thinkDelay = Random.Shared.Next(100, 300);
                await Task.Delay(thinkDelay, cancellationToken);

                // Clique rápido (fast: true) — movimento direto com poucos passos
                await _humanClick.ClickHumanizedAsync(page, clickX, clickY, cancellationToken, fast: true);

                // Log do resultado do clique
                _logger.LogInformation("Round {Round} - Clique #{Index} EXECUTADO em ({ClickX},{ClickY})",
                    round, dotIndex + 1, clickX, clickY);

                // VERIFICAR EXPIRACAO APOS CADA CLIQUE — o CAPTCHA pode expirar
                // entre um clique e outro. Se expirou, abortamos imediatamente
                // em vez de continuar clicando em células que já não existem.
                if (await HasChallengeExpiredAsync(page))
                {
                    _logger.LogWarning("CAPTCHA expirou APOS clique #{Click} no round {Round}. Abortando para retentativa.", dotIndex + 1, round);
                    return false;
                }

                // (Pontos vermelhos de debug removidos — eram visiveis no navegador
                // e ajudavam o Google a detectar automacao)
                dotIndex++;

                // Pausa CURTA entre cliques (sem mover o mouse pra fora do iframe)
                if (ci < matchingBounds.Count - 1)
                {
                    var betweenClickDelay = Random.Shared.Next(100, 250);
                    await Task.Delay(betweenClickDelay, cancellationToken);
                }


            }

            // Pausa após selecionar todas as imagens
            await _humanClick.SimulateThinkingAsync(page, cancellationToken);

            // 7. Verificar expiracao antes de clicar verify
            if (await HasChallengeExpiredAsync(page))
            {
                _logger.LogWarning("CAPTCHA expirou ANTES do verify no round {Round}. Abortando para retentativa.", round);
                return false;
            }

            await _humanClick.SimulateThinkingAsync(page, cancellationToken);

            if (await HasChallengeExpiredAsync(page))
            {
                _logger.LogWarning("CAPTCHA expirou APOS thinking no round {Round}. Abortando para retentativa.", round);
                return false;
            }

            var verifyOk = await ClickVerifyButton(frame, page, cancellationToken);
            if (!verifyOk)
            {
                _logger.LogWarning("ClickVerifyButton retornou false no round {Round} — provavelmente CAPTCHA expirou. Abortando para retentativa.", round);
                return false;
            }

            // 8. Aguardar resposta do Google (3-5s)
            var postVerifyDelay = Random.Shared.Next(3000, 5000);
            _logger.LogInformation("Aguardando {Delay}ms apos verify...", postVerifyDelay);
            await Task.Delay(postVerifyDelay, cancellationToken);

            // Verificar se o grid ainda existe (novas imagens = outro round)
            var newCells = await frame.QuerySelectorAllAsync("td.rc-imageselect-tile");
            if (newCells.Count == 0)
            {
                _logger.LogInformation("Grid desapareceu apos verify no round {Round} - CAPTCHA resolvido!", round);
                return true;
            }

            // Verificar instruction atual
            var instructionElement = await frame.QuerySelectorAsync(".rc-imageselect-desc-no-canonical, .rc-imageselect-desc");
            var currentInstruction = instructionElement != null
                ? await instructionElement.TextContentAsync() ?? ""
                : "";

            _logger.LogInformation("Instruction apos verify round {Round}: {Text}", round, currentInstruction);

            if (round > 1 && currentInstruction != lastInstruction)
            {
                _logger.LogInformation("NOVO DESAFIO detectado no round {Round}", round);
            }

            _logger.LogInformation("IMAGENS ROTACIONADAS no round {Round} - {CellCount} celulas, continuando...",
                round, newCells.Count);

            lastInstruction = currentInstruction;

            // Delay entre rounds para evitar rate limit da OpenAI no n8n.
            // O n8n envia cada celula para OpenAI (gpt-4o-mini) que tem
            // limite de 200.000 TPM. Com 9-16 celulas por round + header,
            // 3 rounds consecutivos estouram o limite rapidamente.
            // So aplica o delay se houver proximo round (evita espera inutil
            // apos o ultimo round).
            if (round < _maxCaptchaRounds)
            {
                var interRoundDelay = Random.Shared.Next(8000, 12000);
                _logger.LogInformation("Delay de {Delay}ms entre rounds para rate limit...", interRoundDelay);
                await Task.Delay(interRoundDelay, cancellationToken);
            }

            // Pausa natural antes do próximo round (humano processa o resultado)
            await _humanClick.SimulateThinkingAsync(page, cancellationToken);
        }

        _logger.LogWarning("Maximo de rounds ({MaxRounds}) atingido sem resolver o CAPTCHA", _maxCaptchaRounds);
        return false;
    }

    /// <summary>
    /// Clica no botao de verificação do CAPTCHA.
    /// ANTES de clicar, verifica se o desafio expirou (checkbox reapareceu).
    /// Se expirou, retorna false em vez de clicar em vão.
    /// </summary>
    private async Task<bool> ClickVerifyButton(IFrame frame, IPage page, CancellationToken cancellationToken)
    {
        try
        {
            // VERIFICAR EXPIRACAO IMEDIATAMENTE ANTES DE CLICAR —
            // o CAPTCHA pode ter expirado entre a última checagem e agora.
            if (await HasChallengeExpiredAsync(page))
            {
                _logger.LogWarning("CAPTCHA expirou ANTES de clicar no botao de verificacao. Abortando.");
                return false;
            }

            var verifyButton = await frame.QuerySelectorAsync("#recaptcha-verify-button, .rc-button-default");
            if (verifyButton != null)
            {
                var verifyBounds = await verifyButton.BoundingBoxAsync();
                if (verifyBounds != null)
                {
                    // Clicar num ponto não-central do botão
                    var vx = (int)(verifyBounds.X + verifyBounds.Width * (0.3 + Random.Shared.NextDouble() * 0.4));
                    var vy = (int)(verifyBounds.Y + verifyBounds.Height * (0.3 + Random.Shared.NextDouble() * 0.4));
                    _logger.LogInformation("Clicando no botao de verificacao em ({X}, {Y})...", vx, vy);
                    await _humanClick.ClickHumanizedAsync(page, vx, vy, cancellationToken);

                    // Verificar se o clique no verify nao fez o CAPTCHA expirar
                    if (await HasChallengeExpiredAsync(page))
                    {
                        _logger.LogWarning("CAPTCHA expirou IMEDIATAMENTE apos clique no verify. Abortando.");
                        return false;
                    }
                }
                else
                {
                    await _humanClick.ClickElementHumanizedAsync(page, verifyButton, cancellationToken);
                }
                // Aguardar processamento do clique (2-4s)
                var afterClickDelay = Random.Shared.Next(2000, 4000);
                await Task.Delay(afterClickDelay, cancellationToken);
                return true;
            }

            _logger.LogWarning("Botao de verificacao nao encontrado no bframe");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao clicar no botao de verificacao");
            return false;
        }
    }
    /// <summary>
    /// Extrai palavras-chave relevantes da instrucao do CAPTCHA para matching.
    /// Mapeia termos em portugues e ingles (ex: "carros" -> "car", "vehicle").
    /// </summary>
    private static List<string> ExtractKeywordsFromInstruction(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return new List<string>();

        var keywords = new List<string>();

        // Fix: inserir espaco antes de letras maiusculas que seguem minusculas
        // (resolve "semáforosSe" -> "semáforos Se")
        var fixedInstruction = System.Text.RegularExpressions.Regex.Replace(
            instruction, "([a-záàâãéèêíïóôõúüç])([A-Z])", "$1 $2");

        var lower = fixedInstruction.ToLowerInvariant();

        // Remover prefixos e sufixos comuns do CAPTCHA
        var removePatterns = new[] {
            "select all", "selecione todas as", "selecione todos os",
            "selecione", "selecionar", "click", "clique",
            "images with", "imagens com", "containing",
            "if there are none, click skip", "se não houver nenhuma, clique em pular",
            "se nao houver nenhuma, clique em pular",
            "click confirm when there are none left",
            "clique em confirmar quando não houver mais nenhuma",
            "clique em confirmar quando nao houver mais nenhuma",
            "when there are none left", "quando não houver mais nenhuma",
            "quando nao houver mais nenhuma", "there are none left",
            "there none left", "none left", "none", "left"
        };
        foreach (var pattern in removePatterns)
        {
            lower = lower.Replace(pattern, " ");
        }

        // Remover pontuacao
        var cleaned = new string(lower.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Stop words em portugues e ingles para ignorar
        var stopWords = new HashSet<string> {
            "a", "an", "the", "um", "uma", "os", "as", "o", "e", "em",
            "de", "da", "do", "das", "dos", "para", "por", "com", "na", "no",
            "se", "que", "mais",            "nenhum", "nenhuma",
            "todos", "todas", "todo", "toda", "quadrados", "quadrado",
            "imagens", "imagem", "clique", "pular", "confirmar",
            "quando", "houver", "não", "nao", "houver",
            "então", "entao", "entao", "nenhuma"
        };

        // Mapeamento PT -> EN para matching com ImageNet labels
        var ptToEn = new Dictionary<string, string[]>
        {
            // Veiculos
            { "carro", new[] { "car", "vehicle", "automobile" } },
            { "carros", new[] { "car", "vehicle", "automobile" } },
            { "caminhao", new[] { "truck", "lorry" } },
            { "onibus", new[] { "bus" } },
            { "moto", new[] { "motorcycle", "motorbike" } },
            { "motos", new[] { "motorcycle", "motorbike" } },
            { "aviacao", new[] { "airplane", "aircraft" } },
            // Infraestrutura
            { "semaforo", new[] { "traffic light", "trafficlight" } },
            { "semaforos", new[] { "traffic light", "trafficlight" } },
            { "hidrante", new[] { "fire hydrant", "hydrant" } },
            { "hidrantes", new[] { "fire hydrant", "hydrant" } },
            { "placa", new[] { "street sign", "sign", "traffic sign" } },
            { "sinal", new[] { "traffic light", "sign" } },
            { "cruzamento", new[] { "crosswalk" } },
            // Animais
            { "cachorro", new[] { "dog" } },
            { "gato", new[] { "cat" } },
            // Pessoas e edificios
            { "pessoa", new[] { "person", "human" } },
            { "pessoas", new[] { "person", "human" } },
            { "predio", new[] { "building" } },
            { "predios", new[] { "building" } },
            { "arvore", new[] { "tree" } },
            { "arvores", new[] { "tree" } },
            // Estradas e infraestrutura
            { "escadas", new[] { "staircase", "stairway", "stairs" } },
            { "escada", new[] { "staircase", "stairway", "stairs" } },
            { "bicicletas", new[] { "bicycle", "bike" } },
            { "bicicleta", new[] { "bicycle", "bike" } },
            { "avião", new[] { "airplane", "aircraft" } },
            { "aviao", new[] { "airplane", "aircraft" } },
            { "trem", new[] { "train" } },
            { "barco", new[] { "boat", "ship" } },
            { "caminhada", new[] { "hiking", "walking" } },
            { "fogão", new[] { "stove", "oven" } },
            { "fogao", new[] { "stove", "oven" } },
            { "ponte", new[] { "bridge" } },
            { "campo", new[] { "field", "meadow" } },
            { "praia", new[] { "beach" } },
            { "montanha", new[] { "mountain" } },
            { "lago", new[] { "lake" } },
            { "rio", new[] { "river" } },
            { "cachoeira", new[] { "waterfall" } },
        };

        foreach (var word in words)
        {
            if (word.Length < 3) continue;
            if (stopWords.Contains(word)) continue;

            keywords.Add(word);

            // Normalizar acentos para lookup no ptToEn (ônibus -> onibus)
            var wordNorm = word.Normalize(System.Text.NormalizationForm.FormD);
            var wordAccentless = new string(wordNorm.Where(c =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray()).Normalize(System.Text.NormalizationForm.FormC);

            if (ptToEn.TryGetValue(wordAccentless, out var enWords))
            {
                keywords.AddRange(enWords);
            }
        }

        return keywords.Distinct().ToList();
    }
}
