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
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
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
            if (content.Contains("tráfego incomum", StringComparison.OrdinalIgnoreCase) || 
                content.Contains("unusual traffic", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("solveSimpleChallenge", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Bloqueio de tráfego incomum detectado pelo Google (IP temporariamente barrado).");
            }

            if (await page.Locator("#captcha").CountAsync() > 0)
            {
                throw new Exception("Bloqueio por captcha detectado. Por favor, resolva o captcha no navegador.");
            }

            foreach (var frame in page.Frames)
            {
                if (frame.Url.Contains("recaptcha", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Bloqueio por captcha (reCAPTCHA) detectado. Por favor, resolva o captcha no navegador.");
                }
            }
        }
        catch (Exception ex) when (ex.Message.Contains("Bloqueio"))
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
