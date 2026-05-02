using Microsoft.Playwright;

namespace AutobookLMM.Extensions;

/// <summary>
/// Reusable low-level extensions for Playwright interfaces.
/// </summary>
public static class PlaywrightExtensions
{
    /// <summary>
    /// Safely waits for a page to settle (network idle) with a maximum timeout.
    /// </summary>
    public static async Task SmartSettleAsync(this IPage page)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 2000 });
        }
        catch { }
        await Task.Delay(500);
    }

    public static async Task PasteImageAsync(this IPage page, string selector, byte[] imageBytes)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        await page.EvaluateAsync(@"(args) => {
            const { base64, selector } = args;
            const byteCharacters = atob(base64);
            const byteNumbers = new Array(byteCharacters.length);
            for (let i = 0; i < byteCharacters.length; i++) byteNumbers[i] = byteCharacters.charCodeAt(i);
            const blob = new Blob([new Uint8Array(byteNumbers)], { type: 'image/png' });
            const fileName = 'pasted_image_' + Date.now() + '.png';
            const file = new File([blob], fileName, { type: 'image/png' });
            const dt = new DataTransfer();
            dt.items.add(file);
            const event = new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true });
            const el = document.querySelector(selector);
            if (el) el.dispatchEvent(event);
        }", new { base64, selector });
    }

    /// <summary>
    /// Wait for a selector to be visible and clicks it.
    /// </summary>
    public static async Task ClickVisibleAsync(this IPage page, string selector, int timeoutMs = 5000)
    {
        var locator = page.Locator(selector).Last;
        await locator.WaitForAsync(new() { Timeout = timeoutMs, State = WaitForSelectorState.Visible });
        await locator.ClickAsync();
    }

    /// <summary>
    /// Wait for a selector to be visible, focuses and fills it.
    /// </summary>
    public static async Task FillVisibleAsync(this IPage page, string selector, string value, int timeoutMs = 5000)
    {
        var locator = page.Locator(selector);
        await locator.WaitForAsync(new() { Timeout = timeoutMs, State = WaitForSelectorState.Visible });
        await locator.FocusAsync();
        await locator.FillAsync(value);
    }

    /// <summary>
    /// Checks if an element is currently visible without throwing.
    /// </summary>
    public static async Task<bool> IsCurrentlyVisibleAsync(this IPage page, string selector)
    {
        try
        {
            return await page.Locator(selector).IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }
}
