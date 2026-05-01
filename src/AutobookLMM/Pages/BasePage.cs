using AutobookLMM.Extensions;
using Microsoft.Playwright;

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
            return await action(await pageFactory());
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
            await action(await pageFactory());
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

        if (!string.IsNullOrEmpty(waitForSelector))
        {
            await page.WaitForSelectorAsync(waitForSelector, new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
    }

    /// <summary>Gets the current URL of the page.</summary>
    public Task<string> GetUrlAsync() => RunAsync(async page => page.Url);
}
