using Domain.Entities;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http.Json;
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
            Headless = true,
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

            if (await _captchaDetection.DetectAsync(page, keyword, cancellationToken))
            {
                // Tentar resolver CAPTCHA com cliques humanizados
                _logger.LogInformation("Tentando resolver CAPTCHA com cliques humanizados...");
                var solved = await TrySolveCaptchaAsync(page, keyword, cancellationToken);

                if (!solved)
                {
                    throw new InvalidOperationException(
                        "CAPTCHA/verificação de segurança detectada pelo Google.");
                }

                _logger.LogInformation("CAPTCHA possivelmente resolvido, continuando...");

                // Verificar novamente se passou
                if (await _captchaDetection.DetectAsync(page, keyword, cancellationToken))
                {
                    throw new InvalidOperationException(
                        "CAPTCHA/verificação de segurança detectada pelo Google.");
                }
            }

            var searchResultElements = await page.QuerySelectorAllAsync("div.g");

            for (int i = 0; i < searchResultElements.Count; i++)
            {
                var element = searchResultElements[i];
                try
                {
                    var linkElement = await element.QuerySelectorAsync("a[href]");
                    if (linkElement == null) continue;

                    var href = await linkElement.GetAttributeAsync("href");
                    if (string.IsNullOrEmpty(href) || !href.StartsWith("http")) continue;

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
                            Position = i + 1,
                            Url = href,
                            Title = title.Trim(),
                            Snippet = snippet.Trim()
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao extrair resultado #{Index}", i + 1);
                }
            }

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
    /// Tenta resolver CAPTCHA: checkbox reCAPTCHA + CAPTCHA de selecao de imagens.
    /// </summary>
    private async Task<bool> TrySolveCaptchaAsync(IPage page, string keyword, CancellationToken cancellationToken)
    {
        try
        {
            var initialUrl = page.Url;
            _logger.LogInformation("URL inicial do CAPTCHA: {Url}", initialUrl);

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
                        _logger.LogInformation("Checkbox encontrado, clicando humanizadamente...");
                        var box = await checkbox.BoundingBoxAsync();
                        if (box != null)
                        {
                            var clickX = (int)(box.X + box.Width / 2);
                            var clickY = (int)(box.Y + box.Height / 2);
                            _logger.LogDebug("Clique no checkbox em ({X}, {Y})", clickX, clickY);
                            await _humanClick.ClickHumanizedAsync(page, clickX, clickY, cancellationToken);
                        }
                        else
                        {
                            await _humanClick.ClickElementHumanizedAsync(page, checkbox, cancellationToken);
                        }

                        // Esperar resposta do Google apos clique
                        _logger.LogInformation("Aguardando resposta do reCAPTCHA apos clique...");
                        await Task.Delay(5000, cancellationToken);
                        _logger.LogInformation("Clique no checkbox reCAPTCHA concluido");

                        // Verificar se abriu CAPTCHA de imagem (grid challenge)
                        await Task.Delay(2000, cancellationToken);
                        var currentUrl = page.Url;
                        _logger.LogInformation("URL apos clique no checkbox: {Url}", currentUrl);

                        if (await HasImageGridChallengeAsync(page))
                        {
                            _logger.LogInformation("CAPTCHA de imagem detectado apos clique no checkbox, tentando resolver...");
                            return await SolveImageGridChallengeAsync(page, cancellationToken);
                        }
                        _logger.LogWarning("Grid de imagem NAO detectada apos clique no checkbox");
                        return true;
                    }
                }
            }

            // Estrategia 2: Resolver CAPTCHA de imagem diretamente (sem checkbox)
            if (await HasImageGridChallengeAsync(page))
            {
                _logger.LogInformation("CAPTCHA de imagem detectado diretamente, tentando resolver...");
                return await SolveImageGridChallengeAsync(page, cancellationToken);
            }

            // Estrategia 3: Clicar no container div.g-recaptcha
            var recaptchaDiv = await page.QuerySelectorAsync("div.g-recaptcha");
            if (recaptchaDiv != null)
            {
                _logger.LogInformation("Div g-recaptcha encontrado, clicando...");
                await _humanClick.ClickElementHumanizedAsync(page, recaptchaDiv, cancellationToken);
                await Task.Delay(5000, cancellationToken);
                return true;
            }

            // Estrategia 4: Clicar no formulario de challenge
            var challengeForm = await page.QuerySelectorAsync("form[action*='challenge']");
            if (challengeForm != null)
            {
                _logger.LogInformation("Challenge form encontrado, clicando...");
                await _humanClick.ClickElementHumanizedAsync(page, challengeForm, cancellationToken);
                await Task.Delay(3000, cancellationToken);
                return true;
            }

            // Estrategia 5: Cliques aleatorios
            var viewportSize = page.ViewportSize;
            var centerX = (viewportSize?.Width ?? 1920) / 2;
            var centerY = (viewportSize?.Height ?? 1080) / 2;

            _logger.LogInformation("Nenhum elemento CAPTCHA encontrado, cliques em regioes variadas...");
            await _humanClick.ClickAroundAsync(page, centerX, centerY, radius: 100, attempts: 8, cancellationToken);
            await Task.Delay(2000, cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tentar resolver CAPTCHA");
            return false;
        }
    }

    /// <summary>
    /// Verifica se ha um CAPTCHA de selecao de imagens (grid) na pagina.
    /// Procura o iframe bframe E o grid dentro dele.
    /// </summary>
    private async Task<bool> HasImageGridChallengeAsync(IPage page)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
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

            if (attempt < 7) await Task.Delay(1500);
        }
        return false;
    }

    /// <summary>
    /// Resolve CAPTCHA de selecao de imagens.
    /// SEMPRE usa o metodo DOM (screenshot cada celula individualmente) que e mais confiavel.
    /// O CaptchaGridAnalyzer produz cortes incorretos para grides 3x3 do Google.
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

        // Multi-round: loop para lidar com "Verifique tambem as novas imagens"
        const int maxRounds = 5;
        var lastInstruction = "";
        for (int round = 1; round <= maxRounds; round++)
        {
            _logger.LogInformation("=== ROUND {Round}/{MaxRounds} ===", round, maxRounds);

            // Re-obter celulas (podem ter mudado apos rotacionar imagens)
            cells = await frame.QuerySelectorAllAsync("td.rc-imageselect-tile");
            if (cells.Count == 0)
            {
                _logger.LogWarning("Nenhuma celula encontrada no round {Round}", round);
                return true; // Grid desapareceu = CAPTCHA resolvido
            }

            _logger.LogInformation("Capturando {Count} celulas (round {Round})...", cells.Count, round);

            // 1. Capturar header (instrucao do CAPTCHA) como base64
            string headerBase64 = "";
            try
            {
                var headerElement = await frame.QuerySelectorAsync(".rc-imageselect-desc-no-canonical, .rc-imageselect-desc");
                if (headerElement != null)
                {
                    var headerScreenshot = await headerElement.ScreenshotAsync();
                    headerBase64 = Convert.ToBase64String(headerScreenshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao capturar header");
            }

            // 2. Capturar cada celula como base64
            var gridBase64List = new List<string>();
            for (int i = 0; i < cells.Count; i++)
            {
                try
                {
                    var cellScreenshot = await cells[i].ScreenshotAsync();
                    gridBase64List.Add(Convert.ToBase64String(cellScreenshot));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao capturar celula {Index}", i);
                    _logger.LogWarning("Falha ao capturar celula {Index}, abortando...", i);
                    return false;
                }
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

                var client = _httpClientFactory.CreateClient("CaptchaWebhook");
                var response = await client.PostAsJsonAsync("", webhookRequest, cancellationToken);
                response.EnsureSuccessStatusCode();

                var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    _logger.LogWarning("WEBHOOK retornou resposta VAZIA no round {Round} - pulando este round", round);
                    continue;
                }

                _logger.LogWarning("WEBHOOK RAW RESPONSE round {Round} ({Length} chars): {Body}", round, rawResponse.Length, rawResponse.Length > 500 ? rawResponse[..500] : rawResponse);

                CaptchaWebhookResponse? webhookResponse = null;
                try
                {
                    webhookResponse = await response.Content.ReadFromJsonAsync<CaptchaWebhookResponse>(cancellationToken: cancellationToken);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Falha ao deserializar JSON do webhook no round {Round}, tentando parse manual...", round);
                }
                if (webhookResponse?.Result == null || webhookResponse.Result.Count == 0)
                {
                    // Parse manual: tentar extrair array de bools do JSON bruto
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
                            _logger.LogWarning("Parse manual OK: {Count} resultados = [{Results}]",
                                boolList.Count, string.Join(", ", boolList));
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogError(parseEx, "Falha no parse manual do JSON do webhook");
                    }
                }

                if (webhookResponse?.Result == null || webhookResponse.Result.Count != cells.Count)
                {
                    _logger.LogWarning("Resposta do webhook invalida: {Count} resultados para {CellCount} celulas",
                        webhookResponse?.Result?.Count ?? 0, cells.Count);
                    return false;
                }

                _logger.LogInformation("Webhook retornou: [{Results}]",
                    string.Join(", ", webhookResponse.Result.Select((r, i) => $"{i}:{r}")));

                // 4. Coletar bounds das celulas onde result[i] == true
                for (int i = 0; i < webhookResponse.Result.Count; i++)
                {
                    if (webhookResponse.Result[i])
                    {
                        var cellBounds = await cells[i].BoundingBoxAsync();
                        if (cellBounds != null)
                        {
                            matchingBounds.Add(cellBounds);
                            _logger.LogWarning("MATCH (webhook): Celula [{Index}] selecionada", i);
                        }
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
                _logger.LogWarning("Nenhuma celula selecionada pelo webhook no round {Round}", round);
                // Pode ser que nao ha mais imagens com o objeto - clicar em verify para finalizar
                _logger.LogInformation("Nenhum match no round {Round}, clicando verify para finalizar...", round);
                await ClickVerifyButton(frame, page, cancellationToken);
                return true;
            }

            _logger.LogInformation("Clicando em {Count} celulas selecionadas pelo webhook (round {Round}, humanizado)...", matchingBounds.Count, round);

            // 5. Clicar nas celulas com comportamento humanizado
            var randomStartX = Random.Shared.Next(200, 600);
            var randomStartY = Random.Shared.Next(200, 400);
            await page.Mouse.MoveAsync(randomStartX, randomStartY);
            await Task.Delay(Random.Shared.Next(500, 1200), cancellationToken);

            var dotIndex = 0;
            for (int ci = 0; ci < matchingBounds.Count; ci++)
            {
                var bounds = matchingBounds[ci];
                var clickX = (int)(bounds.X + bounds.Width / 2);
                var clickY = (int)(bounds.Y + bounds.Height / 2);

                var thinkDelay = Random.Shared.Next(400, 1500);
                await Task.Delay(thinkDelay, cancellationToken);

                await _humanClick.ClickHumanizedAsync(page, clickX, clickY, cancellationToken);

                // Ponto vermelho para debug
                await page.EvaluateAsync($"""
                    const dot = document.createElement('div');
                    dot.style.cssText = 'position:fixed;left:{clickX - 8}px;top:{clickY - 8}px;width:16px;height:16px;background:red;border-radius:50%;z-index:999999;pointer-events:none;border:2px solid white;box-shadow:0 0 4px rgba(0,0,0,0.5);';
                    dot.setAttribute('data-captcha-dot', '{dotIndex}');
                    document.body.appendChild(dot);
                """);
                _logger.LogInformation("Round {Round} - Clique #{Index} em ({X}, {Y}) apos {Delay}ms", round, dotIndex + 1, clickX, clickY, thinkDelay);
                dotIndex++;

                if (ci < matchingBounds.Count - 1)
                {
                    var pauseX = Random.Shared.Next(100, 800);
                    var pauseY = Random.Shared.Next(100, 500);
                    await page.Mouse.MoveAsync(pauseX, pauseY);
                    await Task.Delay(Random.Shared.Next(200, 600), cancellationToken);
                }
            }

            await Task.Delay(Random.Shared.Next(800, 1500), cancellationToken);

            // 6. Screenshot com pontos vermelhos
            var clickDebugDir = Path.Combine(AppContext.BaseDirectory, "temp", "captcha_clicks");
            Directory.CreateDirectory(clickDebugDir);
            var clickScreenshotPath = Path.Combine(clickDebugDir, $"clicks_round{round}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = clickScreenshotPath });
            _logger.LogWarning("SCREENSHOT ROUND {Round} salvo em: {Path}", round, clickScreenshotPath);

            // Remover pontos vermelhos
            await page.EvaluateAsync("""
                document.querySelectorAll('[data-captcha-dot]').forEach(el => el.remove());
            """);

            // 7. Clicar no botao de verificacao
            await ClickVerifyButton(frame, page, cancellationToken);

            // 8. Aguardar e verificar se novas imagens apareceram
            await Task.Delay(2000, cancellationToken);

            // Verificar se o grid ainda existe (novas imagens = multi-round)
            var newCells = await frame.QuerySelectorAllAsync("td.rc-imageselect-tile");
            if (newCells.Count == 0)
            {
                _logger.LogInformation("Grid desapareceu apos verify no round {Round} - CAPTCHA resolvido!", round);
                return true;
            }

            // Verificar se o botao mudou para "Avancar" ou "Pular" (indicando fim do desafio)
            var verifyBtn = await frame.QuerySelectorAsync("#recaptcha-verify-button");
            var btnText = verifyBtn != null ? await verifyBtn.TextContentAsync() ?? "" : "";
            _logger.LogInformation("Botao apos verify round {Round}: '{ButtonText}'", round, btnText);

            // Verificar instruction atual
            var instructionElement = await frame.QuerySelectorAsync(".rc-imageselect-desc-no-canonical, .rc-imageselect-desc");
            var currentInstruction = instructionElement != null
                ? await instructionElement.TextContentAsync() ?? ""
                : "";

            // Comparar com instruction anterior para detectar mudanca de desafio
            _logger.LogInformation("Instruction apos verify round {Round}: {Text}", round, currentInstruction);

            // Se instruction mudou (novo desafio), logar explicitamente
            if (round > 1 && currentInstruction != lastInstruction)
            {
                _logger.LogWarning("NOVO DESAFIO detectado no round {Round}: instruction mudou de '{Old}' para '{New}'",
                    round, lastInstruction, currentInstruction);
            }

            // Log de rotacao de imagens
            _logger.LogWarning("IMAGENS ROTACIONADAS detectadas no round {Round} - {CellCount} celulas ainda presentes, continuando...",
                round, newCells.Count);

            lastInstruction = currentInstruction;

            // Se instruction contem "pular" e nenhuma celula foi selecionada no round anterior, clicar Pular
            if (currentInstruction.Contains("pular", StringComparison.OrdinalIgnoreCase) &&
                currentInstruction.Contains("nenhum", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Desafio permite 'Pular' - verificando se ha mais imagens...");
            }

            // Continua o loop para capturar e classificar as novas imagens
            continue;
        }

        _logger.LogWarning("Maximo de rounds ({MaxRounds}) atingido", maxRounds);
        return true;
    }

    private async Task ClickVerifyButton(IFrame frame, IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var verifyButton = await frame.QuerySelectorAsync("#recaptcha-verify-button, .rc-button-default");
            if (verifyButton != null)
            {
                var verifyBounds = await verifyButton.BoundingBoxAsync();
                if (verifyBounds != null)
                {
                    var vx = (int)(verifyBounds.X + verifyBounds.Width / 2);
                    var vy = (int)(verifyBounds.Y + verifyBounds.Height / 2);
                    _logger.LogInformation("Clicando no botao de verificacao em ({X}, {Y})...", vx, vy);
                    await _humanClick.ClickHumanizedAsync(page, vx, vy, cancellationToken);
                }
                else
                {
                    await _humanClick.ClickElementHumanizedAsync(page, verifyButton, cancellationToken);
                }
                await Task.Delay(2000, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao clicar no botao de verificacao");
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
