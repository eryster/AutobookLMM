using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Models;
using AutobookLMM.Pages;
using AutobookLMM.Managers;
using Microsoft.Playwright;

namespace AutobookLMM.Core;

public class GeminiSession : IGeminiSession
{
    private readonly BrowserContextManager _browserManager = new();
    
    private IPage? _notebookPageHandle;
    private IPage? _chatPageHandle;
    private IPage? _settingsPageHandle;
    private string? _activeNotebookUrl;

    private readonly SemaphoreSlim _notebookLock = new(1, 1);
    private readonly SemaphoreSlim _chatLock = new(1, 1);
    private readonly SemaphoreSlim _settingsLock = new(1, 1);

    public INotebookPage Notebook { get; }
    public INotebookChat Chat { get; }
    public ISettingsPage Settings { get; }

    public bool IsProfileReady => BrowserContextManager.IsProfileReady;

    public GeminiSession()
    {
        Notebook = new NotebookPage(GetNotebookPageAsync, _notebookLock, CaptureDebugAsync);
        Chat = new NotebookChat(GetChatPageAsync, _chatLock, CaptureDebugAsync);
        Settings = new SettingsPage(GetSettingsPageAsync, _settingsLock, CaptureDebugAsync);
    }

    public async Task<bool> CheckLoginAsync()
    {
        var context = await _browserManager.GetContextAsync(forceHeadless: false);
        var page = await BrowserContextManager.GetOrCreatePageAsync(context);
        try
        {
            await page.GotoAsync("https://gemini.google.com/app", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(200);
                var url = page.Url;
                if (url.Contains("accounts.google.com")) { await page.CloseAsync(); return false; }
                if (url.Contains("gemini.google.com") && !url.Contains("signin")) break;
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
                        Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path,
                        ExpirationDate = c.Expires, Secure = c.Secure, HttpOnly = c.HttpOnly
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

    public async Task LoginWithCookiesAsync(string cookiesJson)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "cookies.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, cookiesJson);
        await _browserManager.CloseAsync();
    }

    public Task SetHeadlessAsync(bool headless) => _browserManager.GetContextAsync(forceHeadless: headless);

    public async Task CaptureDebugAsync(string label)
    {
        var page = _notebookPageHandle ?? _chatPageHandle;
        if (page == null || page.IsClosed) return;

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "debug", $"{label}_{DateTime.Now:HHmmss}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
    }

    public void SetActiveNotebook(string url) => _activeNotebookUrl = url;

    public async Task PreloadProjectPagesAsync(string url)
    {
        _activeNotebookUrl = url;
        await Task.WhenAll(GetNotebookPageAsync(), GetChatPageAsync(), GetSettingsPageAsync());
    }

    public async Task CloseNotebookAsync()
    {
        _activeNotebookUrl = null;
        if (_notebookPageHandle != null) await _notebookPageHandle.CloseAsync();
        if (_chatPageHandle != null) await _chatPageHandle.CloseAsync();
        if (_settingsPageHandle != null) await _settingsPageHandle.CloseAsync();
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

    public async Task<INotebookChat> OpenChatAsync(string? url = null)
    {
        var context = await _browserManager.GetContextAsync();
        var page = await context.NewPageAsync();
        
        var targetUrl = url ?? _activeNotebookUrl;
        if (!string.IsNullOrEmpty(targetUrl)) await page.GotoAsync(targetUrl);

        var lockObj = new SemaphoreSlim(1, 1);
        return new NotebookChat(() => Task.FromResult(page), lockObj, CaptureDebugAsync);
    }

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
            if (!string.IsNullOrEmpty(_activeNotebookUrl)) await _chatPageHandle.GotoAsync(_activeNotebookUrl);
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

    public async ValueTask DisposeAsync() => await _browserManager.DisposeAsync();

    public async Task PurgeProfileAsync()
    {
        await _browserManager.CloseAsync();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".autobooklmm", "cookies.json");
        if (File.Exists(path)) File.Delete(path);
    }

    public IAutobookManager CreateManager() => new AutobookManager(this);
}
