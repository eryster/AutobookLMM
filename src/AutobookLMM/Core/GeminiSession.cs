using System.Text.Json;
using AutobookLMM.Abstractions;
using AutobookLMM.Models;
using AutobookLMM.Pages;
using AutobookLMM.Managers;
using Microsoft.Playwright;

namespace AutobookLMM.Core;

/// <summary>
/// Manages the browser lifecycle and underlying page instances for the active Gemini session.
/// </summary>
public class GeminiSession : IGeminiSession
{
    private readonly BrowserContextManager _browserManager;

    private IPage? _notebookPageHandle;
    private IPage? _chatPageHandle;
    private IPage? _settingsPageHandle;
    private string? _activeNotebookUrl;
    public static string? CurrentNotebookUrl { get; set; }

    private readonly SemaphoreSlim _notebookLock = new(1, 1);
    private readonly SemaphoreSlim _chatLock = new(1, 1);
    private readonly SemaphoreSlim _settingsLock = new(1, 1);

    /// <summary>The notebook page instance for the current session.</summary>
    public INotebookPage Notebook { get; }

    /// <summary>The chat instance for interacting with the notebook's AI model.</summary>
    public INotebookChat Chat { get; }

    /// <summary>The settings page instance for adjusting system prompt and notebook configuration.</summary>
    public ISettingsPage Settings { get; }

    /// <summary>Returns true if the user profile has been successfully cached or initialized.</summary>
    public bool IsProfileReady => BrowserContextManager.IsProfileReady;

    /// <summary>
    /// Initializes a new instance of the GeminiSession.
    /// </summary>
    public GeminiSession(bool? headless = null)
    {
        _browserManager = new BrowserContextManager(headless);
        Notebook = new NotebookPage(GetNotebookPageAsync, _notebookLock, CaptureDebugAsync);
        Chat = new NotebookChat(GetChatPageAsync, _chatLock, CaptureDebugAsync);
        Settings = new SettingsPage(GetSettingsPageAsync, _settingsLock, CaptureDebugAsync);
    }

    /// <summary>
    /// Initializes a new instance of the GeminiSession using the provided options.
    /// </summary>
    public GeminiSession(AutobookOptions options) : this(options?.Headless)
    {
        if (options != null && !string.IsNullOrEmpty(options.CookiesJson))
        {
            string json = options.CookiesJson.Trim();
            if (!json.StartsWith('[') && !json.StartsWith('{') && File.Exists(json))
            {
                json = File.ReadAllText(json);
            }
            LoginWithCookiesAsync(json).GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckLoginAsync()
    {
        var context = await _browserManager.GetContextAsync();
        var page = await BrowserContextManager.GetOrCreatePageAsync(context);
        try
        {
            await page.GotoAsync("https://gemini.google.com/app", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(200);
                var url = page.Url;
                if (url.Contains("accounts.google.com")) { await page.CloseAsync(); return false; }
                if ((url.Contains("gemini.google.com") || url.Contains("notebooklm.google.com")) && !url.Contains("signin")) break;
            }

            var loginLink = page.Locator("a[href*='accounts.google.com/ServiceLogin']");
            bool isNotLogged = await loginLink.CountAsync() > 0;

            await page.CloseAsync();
            return !isNotLogged;
        }
        catch
        {
            try { await page.CloseAsync(); } catch { }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task OpenForLoginAsync()
    {
        var tempProfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "login-profile");
        if (Directory.Exists(tempProfile)) { try { Directory.Delete(tempProfile, true); } catch { } }

        using var pw = await Playwright.CreateAsync();
        var options = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            Channel = "chrome",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            Args = ["--disable-blink-features=AutomationControlled", "--no-sandbox"],
            IgnoreDefaultArgs = ["--enable-automation"]
        };

        IBrowserContext loginContext = await pw.Chromium.LaunchPersistentContextAsync(tempProfile, options);
        var page = loginContext.Pages.Count > 0 ? loginContext.Pages[0] : await loginContext.NewPageAsync();

        try
        {
            await page.GotoAsync("https://gemini.google.com/app");
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(3000);
                var isLogged = await page.Locator("a[href*='accounts.google.com/SignOutOptions']").CountAsync() > 0;
                if (isLogged)
                {
                    await Task.Delay(5000);
                    var cookies = await loginContext.CookiesAsync();
                    var cookieDtos = cookies.Select(c => new CookieDto
                    {
                        Name = c.Name,
                        Value = c.Value,
                        Domain = c.Domain,
                        Path = c.Path,
                        ExpirationDate = c.Expires,
                        Secure = c.Secure,
                        HttpOnly = c.HttpOnly
                    }).ToList();

                    var json = JsonSerializer.Serialize(cookieDtos);
                    await LoginWithCookiesAsync(json);
                    return;
                }
            }
            throw new TimeoutException("Login timeout");
        }
        finally
        {
            await loginContext.CloseAsync();
        }
    }

    /// <inheritdoc />
    public async Task LoginWithCookiesAsync(string cookiesJson)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "cookies.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, cookiesJson);
        await _browserManager.CloseAsync();
    }

    /// <inheritdoc />
    public Task SetHeadlessAsync(bool headless) => _browserManager.GetContextAsync(forceHeadless: headless);

    /// <inheritdoc />
    public async Task CaptureDebugAsync(string label)
    {
        var page = _notebookPageHandle ?? _chatPageHandle;
        if (page == null || page.IsClosed) return;

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "debug", $"{label}_{DateTime.Now:HHmmss}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
    }

    /// <inheritdoc />
    public void SetActiveNotebook(string url)
    {
        _activeNotebookUrl = url;
        CurrentNotebookUrl = url;
    }

    /// <inheritdoc />
    public async Task PreloadProjectPagesAsync(string url)
    {
        _activeNotebookUrl = url;
        CurrentNotebookUrl = url;
        await Task.WhenAll(GetNotebookPageAsync(), GetChatPageAsync(), GetSettingsPageAsync());
    }

    /// <inheritdoc />
    public async Task CloseNotebookAsync()
    {
        _activeNotebookUrl = null;
        CurrentNotebookUrl = null;
        if (_notebookPageHandle != null) try { await _notebookPageHandle.CloseAsync(); } catch { }
        if (_chatPageHandle != null) try { await _chatPageHandle.CloseAsync(); } catch { }
        if (_settingsPageHandle != null) try { await _settingsPageHandle.CloseAsync(); } catch { }
        _notebookPageHandle = null;
        _chatPageHandle = null;
        _settingsPageHandle = null;
    }

    public async Task CloseChatAsync()
    {
        if (_chatPageHandle != null)
        {
            try { await _chatPageHandle.CloseAsync(); } catch { }
            _chatPageHandle = null;
        }
    }

    private async Task<IPage> GetNotebookPageAsync()
    {
        var context = await _browserManager.GetContextAsync();
        if (_notebookPageHandle == null || _notebookPageHandle.IsClosed)
        {
            _notebookPageHandle = await BrowserContextManager.GetOrCreatePageAsync(context);
            _notebookPageHandle.Close += (_, _) => _notebookPageHandle = null;
            if (!string.IsNullOrEmpty(_activeNotebookUrl)) await _notebookPageHandle.GotoAsync(_activeNotebookUrl);
        }
        return _notebookPageHandle;
    }

    /// <inheritdoc />
    public async Task<INotebookChat> OpenChatAsync(string? url = null)
    {
        var context = await _browserManager.GetContextAsync();
        var page = await context.NewPageAsync();

        var targetUrl = url ?? _activeNotebookUrl;
        if (!string.IsNullOrEmpty(targetUrl)) await page.GotoAsync(targetUrl);

        var lockObj = new SemaphoreSlim(1, 1);
        return new NotebookChat(() => Task.FromResult(page), lockObj, CaptureDebugAsync);
    }

    /// <inheritdoc />
    public async Task<ISettingsPage> OpenSettingsAsync()
    {
        var context = await _browserManager.GetContextAsync();
        var page = await context.NewPageAsync();

        if (!string.IsNullOrEmpty(_activeNotebookUrl)) await page.GotoAsync(_activeNotebookUrl);

        var lockObj = new SemaphoreSlim(1, 1);
        return new SettingsPage(() => Task.FromResult(page), lockObj, CaptureDebugAsync);
    }

    private async Task<IPage> GetChatPageAsync()
    {
        var context = await _browserManager.GetContextAsync();
        if (_chatPageHandle == null || _chatPageHandle.IsClosed)
        {
            _chatPageHandle = await context.NewPageAsync();
            _chatPageHandle.Close += (_, _) => _chatPageHandle = null;
            if (!string.IsNullOrEmpty(_activeNotebookUrl)) await _chatPageHandle.GotoAsync(_activeNotebookUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 10000 });
        }
        return _chatPageHandle;
    }

    private async Task<IPage> GetSettingsPageAsync()
    {
        var context = await _browserManager.GetContextAsync();
        if (_settingsPageHandle == null || _settingsPageHandle.IsClosed)
        {
            _settingsPageHandle = await context.NewPageAsync();
            _settingsPageHandle.Close += (_, _) => _settingsPageHandle = null;
            if (!string.IsNullOrEmpty(_activeNotebookUrl)) await _settingsPageHandle.GotoAsync(_activeNotebookUrl);
        }
        return _settingsPageHandle;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _browserManager.DisposeAsync();

    /// <inheritdoc />
    public async Task PurgeProfileAsync()
    {
        await _browserManager.CloseAsync();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "cookies.json");
        if (File.Exists(path)) File.Delete(path);
    }

    /// <inheritdoc />
    public IAutobookManager CreateManager(bool? headless = null)
    {
        if (headless.HasValue)
        {
            _browserManager.SetHeadless(headless.Value);
        }
        return new AutobookManager(this);
    }
}
