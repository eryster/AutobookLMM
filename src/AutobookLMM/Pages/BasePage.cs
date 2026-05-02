using AutobookLMM.Extensions;
using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutobookLMM.Pages;

/// <summary>
/// Base class for all page implementations.
/// </summary>
public abstract class BasePage(
    Func<Task<IPage>> pageFactory,
    SemaphoreSlim pageLock,
    string debugPrefix,
    Func<string, Task>? onDebug = null)
{
    protected async Task<T> RunAsync<T>(Func<IPage, Task<T>> action)
    {
        await pageLock.WaitAsync();
        try
        {
            var page = await pageFactory();
            await CheckForGoogleBlocksAsync(page);
            return await action(page);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (onDebug != null) await onDebug($"{debugPrefix}_error");
            throw;
        }
        finally
        {
            pageLock.Release();
        }
    }

    protected async Task RunAsync(Func<IPage, Task> action)
    {
        await pageLock.WaitAsync();
        try
        {
            var page = await pageFactory();
            await CheckForGoogleBlocksAsync(page);
            await action(page);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (onDebug != null) await onDebug($"{debugPrefix}_error");
            throw;
        }
        finally
        {
            pageLock.Release();
        }
    }

    protected async Task NavigateAsync(IPage page, string url, string? waitForSelector = null)
    {
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 5000 });
        await page.SmartSettleAsync();
        await CheckForGoogleBlocksAsync(page);

        if (!string.IsNullOrEmpty(waitForSelector))
        {
            await page.WaitForSelectorAsync(waitForSelector, new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
    }

    protected async Task CheckForGoogleBlocksAsync(IPage page)
    {
        try
        {
            var content = await page.ContentAsync();
            if (content.Contains("incomum", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("unusual", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("solveSimpleChallenge", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("captcha", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Google detected unusual traffic (IP temporarily blocked).");
            }

            if (await page.Locator("#captcha").CountAsync() > 0)
            {
                throw new Exception("Google captcha challenge detected. Please solve the captcha in the browser.");
            }

            foreach (var frame in page.Frames)
            {
                if (frame.Url.Contains("recaptcha", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Google captcha challenge detected. Please solve the captcha in the browser.");
                }
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Google"))
        {
            throw;
        }
        catch
        {
            // Squelch other errors
        }
    }

    /// <summary>Gets the current URL of the page.</summary>
    public Task<string> GetUrlAsync() => RunAsync(async page => page.Url);
}
