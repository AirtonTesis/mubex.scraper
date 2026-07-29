using System.IO;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Scraping;

/// <summary>
/// Interface para detecção de CAPTCHA nas páginas de busca.
/// </summary>
public interface ICaptchaDetectionService
{
    Task<bool> DetectAsync(IPage page, string? screenshotPrefix = null, CancellationToken cancellationToken = default);
}

public class CaptchaDetectionService : ICaptchaDetectionService
{
    private readonly ILogger<CaptchaDetectionService> _logger;

    private readonly string _screenshotDir;

    public CaptchaDetectionService(ILogger<CaptchaDetectionService> logger)
    {
        _logger = logger;
        _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDir);
    }

    public async Task<bool> DetectAsync(IPage page, string? screenshotPrefix = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Aguardar pagina estabilizar antes de ler conteudo
            try
            {
                await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 5000 });
            }
            catch { /* Timeout e OK */ }

            var currentUrl = page.Url;
            var content = await page.ContentAsync();
            var title = await page.TitleAsync();

            _logger.LogDebug("Verificando CAPTCHA - URL: {Url}, Título: {Title}", currentUrl, title);

            // 1. Verificar se URL indica bloqueio
            if (currentUrl.Contains("sorry", StringComparison.OrdinalIgnoreCase) ||
                currentUrl.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
                currentUrl.Contains("challenge", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("URL de bloqueio detectada: {Url}", currentUrl);
                await SaveScreenshotAsync(page, screenshotPrefix, "blocked_url");
                return true;
            }

            // 2. Verificar reCAPTCHA interativo (div.g-recaptcha)
            var recaptchaDiv = await page.QuerySelectorAsync("div.g-recaptcha");
            if (recaptchaDiv != null)
            {
                _logger.LogWarning("reCAPTCHA div interativo detectado");
                await SaveScreenshotAsync(page, screenshotPrefix, "recaptcha_div");
                return true;
            }

            // 3. Verificar iframe de reCAPTCHA com src de anchor/enterprise
            var recaptchaIframe = await page.QuerySelectorAsync("iframe[src*='recaptcha/enterprise'], iframe[src*='recaptcha/anchor']");
            if (recaptchaIframe != null)
            {
                _logger.LogWarning("Iframe reCAPTCHA Enterprise detectado");
                await SaveScreenshotAsync(page, screenshotPrefix, "recaptcha_iframe");
                return true;
            }

            // 4. Verificar texto de bloqueio no conteúdo
            if (content.Contains("unusual traffic", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("automated requests", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("não é um robô", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("verificação de segurança", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("To continue, please", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("please complete the security check", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("consent.google.com", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Mensagem de bloqueio detectada no conteúdo");
                await SaveScreenshotAsync(page, screenshotPrefix, "blocked_content");
                return true;
            }

            // 5. Verificar formulário de challenge
            var challengeForm = await page.QuerySelectorAsync("form[action*='challenge']");
            if (challengeForm != null)
            {
                _logger.LogWarning("Challenge form detectado");
                await SaveScreenshotAsync(page, screenshotPrefix, "challenge_form");
                return true;
            }

            // 6. Verificar se não há resultados e título indica problema
            var searchResults = await page.QuerySelectorAllAsync("div.g");
            if (searchResults.Count == 0)
            {
                if (title.Contains("sorry", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("unusual traffic", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Google", StringComparison.OrdinalIgnoreCase) && currentUrl.Contains("sorry"))
                {
                    _logger.LogWarning("Página sem resultados com título de bloqueio: {Title}", title);
                    await SaveScreenshotAsync(page, screenshotPrefix, "empty_results");
                    return true;
                }

                // Sem div.g mas título limpo — pode ser pesquisa legítima sem resultados
                _logger.LogDebug("Página sem resultados div.g mas título limpo (título: {Title})", title);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar CAPTCHA");
            return false;
        }
    }

    private async Task SaveScreenshotAsync(IPage page, string? prefix, string reason)
    {
        try
        {
            var safePrefix = string.Join("_", (prefix ?? "captcha").Split(Path.GetInvalidFileNameChars()));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"{safePrefix}_{reason}_{timestamp}.png";
            var filePath = Path.Combine(_screenshotDir, filename);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = filePath,
                FullPage = true
            });

            _logger.LogWarning("Screenshot salvo: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar screenshot do CAPTCHA");
        }
    }
}
