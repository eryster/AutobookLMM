using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Models;
using Microsoft.Playwright;

namespace AutobookLMM.Core;

/// <summary>
/// Internal helper for GeminiSession to manage Playwright lifecycle.
/// </summary>
public class BrowserContextManager : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private bool? _currentHeadless = false;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BrowserContextManager(bool? initialHeadless = null)
    {
        _currentHeadless = initialHeadless;
    }

    public void SetHeadless(bool headless)
    {
        if (_currentHeadless != headless)
        {
            _currentHeadless = headless;
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string CookieFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "cookies.json");

    public static bool IsProfileReady => File.Exists(CookieFilePath);

    public async Task<IBrowserContext> GetContextAsync(bool? forceHeadless = null)
    {
        await _lock.WaitAsync();
        try
        {
            var headless = forceHeadless ?? _currentHeadless ?? false;

            if (_context != null && _currentHeadless != headless)
            {
                await CloseAsync();
            }

            _playwright ??= await Playwright.CreateAsync();
            if (_browser == null)
            {
                var launchOptions = new BrowserTypeLaunchOptions
                {
                    Headless = headless,
                    Channel = "chrome",
                    Args = new[] {
                        "--disable-blink-features=AutomationControlled",
                        "--no-sandbox",
                        "--disable-dev-shm-usage",
                        "--enable-gpu",
                        "--ignore-gpu-blocklist",
                        "--enable-webgl",
                        "--disable-extensions",
                        "--disable-plugins-discovery",
                        "--disable-infobars"
                    },
                    IgnoreDefaultArgs = new[] { "--enable-automation" }
                };

                try
                {
                    _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
                }
                catch (Exception ex) when (ex.Message.Contains("Executable doesn't exist"))
                {
                    await InstallBrowserAsync();
                    _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
                }
            }

            if (_context == null)
            {
                var locale = System.Globalization.CultureInfo.CurrentCulture.Name;
                var baseLang = locale.Split('-')[0];

                _currentHeadless = headless;
                _context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                    ViewportSize = null,
                    Locale = locale,
                    ExtraHTTPHeaders = new Dictionary<string, string>
                    {
                        { "sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"" },
                        { "sec-ch-ua-mobile", "?0" },
                        { "sec-ch-ua-platform", "\"Windows\"" },
                        { "sec-ch-ua-platform-version", "\"15.0.0\"" },
                        { "sec-ch-ua-model", "\"\"" }
                    }
                });

                await _context.AddInitScriptAsync($@"
                    // Hide automation
                    Object.defineProperty(navigator, 'webdriver', {{ get: () => undefined }});
                    
                    // Mock Chrome specific properties
                    window.chrome = {{
                        runtime: {{}},
                        loadTimes: function() {{ return {{}}; }},
                        csi: function() {{ return {{}}; }},
                        app: {{
                            isInstalled: false,
                            InstallState: {{ DISABLED: 'disabled', INSTALLED: 'installed', NOT_INSTALLED: 'not_installed' }},
                            RunningState: {{ CANNOT_RUN: 'cannot_run', READY_TO_RUN: 'ready_to_run', RUNNING: 'running' }}
                        }}
                    }};

                    // Enhanced Plugins spoofing
                    const mockPlugins = [
                        {{ name: 'PDF Viewer', filename: 'internal-pdf-viewer', description: 'Portable Document Format' }},
                        {{ name: 'Chrome PDF Viewer', filename: 'internal-pdf-viewer', description: 'Portable Document Format' }},
                        {{ name: 'Chromium PDF Viewer', filename: 'internal-pdf-viewer', description: 'Portable Document Format' }},
                        {{ name: 'Microsoft Edge PDF Viewer', filename: 'internal-pdf-viewer', description: 'Portable Document Format' }},
                        {{ name: 'WebKit built-in PDF', filename: 'internal-pdf-viewer', description: 'Portable Document Format' }}
                    ];
                    Object.defineProperty(navigator, 'plugins', {{ get: () => mockPlugins }});

                    // Languages & Hardware
                    Object.defineProperty(navigator, 'languages', {{ get: () => ['{locale}', '{baseLang}', 'en-US'] }});
                    Object.defineProperty(navigator, 'hardwareConcurrency', {{ get: () => 8 }});
                    Object.defineProperty(navigator, 'deviceMemory', {{ get: () => 8 }});

                    // Permissions override
                    const originalQuery = window.navigator.permissions.query;
                    window.navigator.permissions.query = (params) =>
                        params.name === 'notifications'
                            ? Promise.resolve({{ state: Notification.permission }})
                            : originalQuery(params);

                    // WebGL Fingerprint spoofing
                    const getParameter = WebGLRenderingContext.prototype.getParameter;
                    WebGLRenderingContext.prototype.getParameter = function(parameter) {{
                        // UNMASKED_VENDOR_WEBGL
                        if (parameter === 37445) return 'Google Inc. (NVIDIA)';
                        // UNMASKED_RENDERER_WEBGL
                        if (parameter === 37446) return 'ANGLE (NVIDIA, NVIDIA GeForce RTX 3080 Direct3D11 vs_5_0 ps_5_0, D3D11)';
                        return getParameter.apply(this, arguments);
                    }};

                    // Broken iframes fix
                    const iframeWindow = HTMLIFrameElement.prototype.contentWindow;
                    Object.defineProperty(HTMLIFrameElement.prototype, 'contentWindow', {{
                        get: function() {{
                            const window = iframeWindow.apply(this);
                            if (this.srcdoc) return window;
                            try {{
                                if (window.navigator.webdriver !== undefined) {{
                                    Object.defineProperty(window.navigator, 'webdriver', {{ get: () => undefined }});
                                }}
                            }} catch (e) {{}}
                            return window;
                        }}
                    }});
                ");

                _context.SetDefaultTimeout(5000);
                _context.SetDefaultNavigationTimeout(30000);

                await InjectCookiesAsync(_context);
                _context.Close += (_, _) => { _context = null; };
            }

            return _context;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task InjectCookiesAsync(IBrowserContext context)
    {
        if (!File.Exists(CookieFilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(CookieFilePath);
            var rawCookies = JsonSerializer.Deserialize<List<CookieDto>>(json, _jsonOptions);
            if (rawCookies == null) return;

            var mappedCookies = rawCookies.Select(c => new Cookie
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Expires = c.ExpirationDate != null ? (float?)c.ExpirationDate : (c.Expires != null ? (float?)c.Expires : null),
                HttpOnly = c.HttpOnly ?? false,
                Secure = c.Secure ?? false,
                SameSite = c.SameSite?.ToLower() switch
                {
                    "strict" => SameSiteAttribute.Strict,
                    "lax" => SameSiteAttribute.Lax,
                    _ => SameSiteAttribute.None
                }
            }).ToList();

            await context.AddCookiesAsync(mappedCookies);
        }
        catch { }
    }

    public async Task CloseAsync()
    {
        if (_context != null) { try { await _context.CloseAsync(); } catch { } }
        if (_browser != null) { try { await _browser.CloseAsync(); } catch { } }
        _context = null;
        _browser = null;
        _currentHeadless = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _playwright?.Dispose();
        _playwright = null;
        GC.SuppressFinalize(this);
    }

    public static async Task InstallBrowserAsync()
    {
        var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playwright.ps1");
        if (!File.Exists(script)) return;

        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{script}\" install",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        if (proc != null) await proc.WaitForExitAsync();
    }

    public static async Task<IPage> GetOrCreatePageAsync(IBrowserContext context, IPage? exclude = null)
    {
        var blank = context.Pages.FirstOrDefault(p =>
            p != exclude &&
            (string.IsNullOrEmpty(p.Url) || p.Url == "about:blank"));

        return blank ?? await context.NewPageAsync();
    }
}
