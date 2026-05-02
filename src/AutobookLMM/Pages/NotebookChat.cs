using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Extensions;
using AutobookLMM.Models;
using Microsoft.Playwright;

namespace AutobookLMM.Pages;

/// <summary>
/// Provides operations for interacting with a specific notebook chat tab.
/// </summary>
public class NotebookChat(
    Func<Task<IPage>> pageFactory,
    SemaphoreSlim pageLock,
    Func<string, Task>? onDebug = null) : BasePage(pageFactory, pageLock, "chat", onDebug), INotebookChat
{
    private const string ChatInputSelector = ".new-input-ui";
    private const string ResponseContentSelector = "[id^=\"model-response-message\"]";
    private const string StopButtonSelector = ".stop";
    private const string LegacyThinkingSelector = ".thinking, pending-request, pending-request-dot-animation, canvas";
    private const string ConversationTitleSelector = "[data-test-id=\"conversation-title\"]";

    // Management Selectors
    private const string ChatTitleSelector = "[data-test-id=\"chat-title\"]";
    private const string ChatDeleteBtnSelector = "[data-test-id=\"delete-button\"]";
    private const string ConfirmButtonSelector = "[data-test-id=\"confirm-button\"]";
    private const string ImageUploadBtnSelector = "[data-test-id=\"upload-image-button\"], button[aria-label*=\"image\"]";
    private const string ImageLoadingPreviewSelector = "[data-test-id=\"image-loading-preview\"]";

    private int _initialResponseCount;

    /// <inheritdoc />
    public async Task<string> SendMessageAsync(string message, IEnumerable<byte[]>? images = null, Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200)
    {
        await SubmitAsync(message, images);
        return await GetResponseAsync(onChunk, extractionScript, timeoutSeconds, pollingIntervalMs);
    }

    /// <inheritdoc />
    public Task NavigateToUrlAsync(string url) => RunAsync(page => NavigateAsync(page, url));

    /// <inheritdoc />
    public Task<string> GetTitleAsync() =>
        RunAsync(async page =>
        {
            var locator = page.Locator(ConversationTitleSelector);
            if (await locator.CountAsync() > 0)
            {
                var text = await locator.InnerTextAsync();
                return text.Trim();
            }
            return "Untitled Conversation";
        });

    public Task SubmitAsync(string message, IEnumerable<byte[]>? images = null) =>
        RunAsync(async page =>
        {
            await page.BringToFrontAsync();

            _initialResponseCount = await page.Locator(ResponseContentSelector).CountAsync();

            var input = page.Locator(ChatInputSelector);
            await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await input.FocusAsync();

            // 2. Paste images if any
            if (images != null)
            {
                foreach (var img in images)
                {
                    await page.PasteImageAsync(ChatInputSelector, img);
                }

                // Wait for all images to finish loading (preview disappears)
                try
                {
                    await page.WaitForSelectorAsync(ImageLoadingPreviewSelector,
                        new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
                }
                catch { /* If it never appeared or already disappeared, we continue */ }
            }

            // 3. Fill text
            await input.FillAsync(message);

            // 4. Submit
            await page.Keyboard.PressAsync("Enter");
        });

    public Task TypeMessageAsync(string text, bool pressEnter = false, IEnumerable<byte[]>? images = null) =>
        RunAsync(async page =>
        {
            await page.BringToFrontAsync();
            var input = page.Locator(ChatInputSelector);
            await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await input.FocusAsync();

            // Paste images if any
            if (images != null)
            {
                foreach (var img in images)
                {
                    await page.PasteImageAsync(ChatInputSelector, img);
                }

                // Wait for all images to finish loading (preview disappears)
                try
                {
                    await page.WaitForSelectorAsync(ImageLoadingPreviewSelector,
                        new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
                }
                catch { /* If it never appeared or already disappeared, we continue */ }
            }

            await input.FillAsync(text);
            if (pressEnter)
            {
                await page.Keyboard.PressAsync("Enter");
            }
        });

    /// <inheritdoc />
    public Task PasteImagesAsync(IEnumerable<byte[]> images) =>
        RunAsync(async page =>
        {
            await page.BringToFrontAsync();
            var input = page.Locator(ChatInputSelector);
            await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await input.FocusAsync();

            if (images != null)
            {
                foreach (var img in images)
                {
                    await page.PasteImageAsync(ChatInputSelector, img);
                }

                try
                {
                    await page.WaitForSelectorAsync(ImageLoadingPreviewSelector,
                        new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
                }
                catch { }
            }
        });

    /// <inheritdoc />
    public async Task<string> GetResponseAsync(Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200)
    {
        string lastFullText = "";
        await foreach (var chunk in StreamResponseAsync(extractionScript, timeoutSeconds, pollingIntervalMs))
        {
            lastFullText = chunk;
            onChunk?.Invoke(chunk);
        }
        return lastFullText;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200)
    {
        await pageLock.WaitAsync();
        try
        {
            var page = await pageFactory();
            var responseLocator = page.Locator(ResponseContentSelector).Last;
            var script = extractionScript ?? ExtractionScript;

            // 1. Wait for a new response element to appear
            DateTime syncStart = DateTime.Now;
            bool newResponseFound = false;
            while ((DateTime.Now - syncStart).TotalSeconds < 30)
            {
                var currentCount = await page.Locator(ResponseContentSelector).CountAsync();
                if (currentCount > _initialResponseCount)
                {
                    newResponseFound = true;
                    break;
                }
                await Task.Delay(200);
            }

            if (!newResponseFound) throw new TimeoutException("Gemini response did not start in time.");

            // 2. Wait for the response content to start appearing
            DateTime triggerStart = DateTime.Now;
            bool ready = false;
            while ((DateTime.Now - triggerStart).TotalSeconds < 40)
            {
                if (await responseLocator.CountAsync() > 0)
                {
                    var text = "";
                    try { text = await responseLocator.InnerTextAsync(); } catch { }
                    if (await IsGenerating(page) || text.Length > 0) { ready = true; break; }
                }
                await Task.Delay(100);
            }

            if (!ready) throw new Exception("Gemini response element failed to appear.");

            // 3. Poll for chunks
            string lastText = "";
            DateTime pollStart = DateTime.Now;

            while ((DateTime.Now - pollStart).TotalSeconds < timeoutSeconds)
            {
                string textNow = "";
                try { textNow = await responseLocator.EvaluateAsync<string>(script); }
                catch { await Task.Delay(pollingIntervalMs); continue; }

                if (!string.IsNullOrEmpty(textNow) && textNow != lastText)
                {
                    lastText = textNow;
                    yield return lastText.Trim();
                }

                bool uiGenerating = await IsGenerating(page);
                if (!uiGenerating)
                {
                    await Task.Delay(500);
                    // Final extraction
                    try { textNow = await responseLocator.EvaluateAsync<string>(script); } catch { }
                    yield return textNow.Trim();
                    yield break;
                }

                await Task.Delay(pollingIntervalMs);
            }

            throw new TimeoutException("Gemini response timed out.");
        }
        finally
        {
            pageLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<List<ChatMetadata>> ListChatsAsync() =>
        RunAsync(async page =>
        {
            await EnsureInGuideAsync(page);

            var locators = await page.Locator(ChatTitleSelector).AllAsync();
            var results = new List<ChatMetadata>();

            foreach (var loc in locators)
            {
                var title = await loc.InnerTextAsync();

                // Em NotebookLM, o título geralmente está dentro ou é vizinho do link <a>
                var link = page.Locator("a").Filter(new() { Has = loc }).First;
                var href = await link.GetAttributeAsync("href") ?? "";

                // Converte URL relativa em absoluta se necessário
                if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                {
                    href = new Uri(new Uri(page.Url), href).ToString();
                }

                results.Add(new ChatMetadata
                {
                    Title = title.Trim(),
                    Url = href
                });
            }

            return results;
        });

    /// <inheritdoc />
    public Task DeleteChatAsync(string title) =>
        RunAsync(async page =>
        {
            await EnsureInGuideAsync(page);

            try
            {
                await page.Locator(".project-chat-row-container").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }
            catch { }

            var allChats = await page.Locator(".project-chat-row-container").AllAsync();
            ILocator? chatItem = null;
            foreach (var loc in allChats)
            {
                var txt = await loc.InnerTextAsync();
                if (txt.Contains(title, StringComparison.OrdinalIgnoreCase))
                {
                    chatItem = loc;
                    break;
                }
            }

            if (chatItem == null)
            {
                throw new Exception($"Chat with title '{title}' was not found.");
            }

            await chatItem.HoverAsync();
            var menuBtn = chatItem.Locator("button").First;
            try
            {
                await menuBtn.EvaluateAsync("el => el.click()");
            }
            catch
            {
                await menuBtn.ClickAsync(new() { Force = true });
            }

            await page.ClickVisibleAsync(ChatDeleteBtnSelector);
            await page.ClickVisibleAsync(ConfirmButtonSelector);

            await chatItem.WaitForAsync(new()
            {
                State = WaitForSelectorState.Hidden
            });
        });

    /// <inheritdoc />
    public Task<bool> OpenChatByTitleAsync(string title) =>
        RunAsync(async page =>
        {
            await EnsureInGuideAsync(page);

            try
            {
                await page.Locator(".project-chat-row-container").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }
            catch { }

            var allChats = await page.Locator(".project-chat-row-container").AllAsync();
            ILocator? chatItem = null;
            foreach (var loc in allChats)
            {
                var txt = await loc.InnerTextAsync();
                if (txt.Contains(title, StringComparison.OrdinalIgnoreCase))
                {
                    chatItem = loc;
                    break;
                }
            }

            if (chatItem == null)
            {
                return false;
            }

            await chatItem.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            return true;
        });

    private async Task EnsureInGuideAsync(IPage page)
    {
        var notebookUrl = AutobookLMM.Core.GeminiSession.CurrentNotebookUrl;
        if (string.IsNullOrEmpty(notebookUrl) && page.Url.Contains("/app/"))
        {
            notebookUrl = page.Url.Replace("/app/", "/notebook/");
        }

        if (!string.IsNullOrEmpty(notebookUrl) && page.Url.Contains("/app/"))
        {
            await page.GotoAsync(notebookUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 10000 });
            await page.SmartSettleAsync();
        }
    }

    private async Task<bool> IsGenerating(IPage page)
    {
        try
        {
            if (await page.Locator(StopButtonSelector).IsVisibleAsync()) return true;
            if (await page.Locator(LegacyThinkingSelector).IsVisibleAsync()) return true;
            return false;
        }
        catch { return false; }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await pageLock.WaitAsync();
        try
        {
            var page = await pageFactory();
            if (page != null && !page.IsClosed) await page.CloseAsync();
        }
        catch { }
        finally { pageLock.Release(); }
    }

    private const string ExtractionScript = @"el => {
        const root = el.querySelector('[id^=""model-response-message""]') || el;
        const NOISE = new Set(['source-footnote','sup','sources-carousel-inline','button','mat-icon','script','style', 'code-block-decoration', 'suggested-change-actions']);

        function walk(node) {
            if (!node) return '';
            if (node.nodeType === 3) return node.textContent;
            if (node.nodeType !== 1) return '';

            const tag = node.tagName.toLowerCase();
            const className = node.className || '';
            if (NOISE.has(tag) || (typeof className === 'string' && className.includes('suggested-change-actions'))) return '';

            if (tag === 'suggested-change' || className.includes('suggested-change')) {
                const fileEl = node.querySelector('.file-name, [class*=""file-path""]');
                const filePath = fileEl ? fileEl.innerText.trim() : 'unknown_file';
                const codeEl = node.querySelector('.new-code, code, pre');
                const content = codeEl ? codeEl.innerText.trim() : '';
                
                return `\n\n<code_change_tool>\n    <file_path>${filePath}</file_path>\n    <replace_line_ref></replace_line_ref>\n    <new_code_changes>\n${content}\n    </new_code_changes>\n</code_change_tool>\n\n`;
            }

            if (tag === 'table') {
                const rows = Array.from(node.querySelectorAll('tr'));
                if (rows.length === 0) return '';
                const mdRows = rows.map((tr, i) => {
                    const cells = Array.from(tr.querySelectorAll('td, th'));
                    const line = '| ' + cells.map(c => walk(c).replace(/\n+/g, ' ').trim()).join(' | ') + ' |';
                    if (i === 0) return line + '\n| ' + cells.map(() => '---').join(' | ') + ' |';
                    return line;
                });
                return '\n\n' + mdRows.join('\n') + '\n\n';
            }

            if (tag === 'code-block' || tag === 'pre') {
                const codeEl = node.querySelector('code[data-test-id=""code-content""]') || 
                               node.querySelector('code') || 
                               node.querySelector('pre') || node;
                const langEl = node.querySelector('.code-block-decoration span');
                const lang = (langEl ? langEl.innerText : '').trim().toLowerCase();
                const raw = codeEl.innerText.trim();
                return '\n\n```' + lang + '\n' + raw + '\n```\n\n';
            }

            if (tag === 'li') {
                let inner = Array.from(node.childNodes).map(walk).join('').trim();
                const prefix = node.closest('ol') ? '1. ' : '- ';
                return prefix + inner + '\n';
            }

            let inner = Array.from(node.childNodes).map(walk).join('');

            if (tag === 'br' || tag === 'hr') return '\n';
            if (tag.match(/^h[1-6]$/)) return '\n' + '#'.repeat(parseInt(tag[1])) + ' ' + inner.trim() + '\n';
            if (tag === 'p') {
                if (node.parentElement && node.parentElement.tagName.toLowerCase() === 'li') return inner.trim();
                return '\n' + inner.trim() + '\n';
            }
            if (tag === 'ul' || tag === 'ol') return '\n' + inner.trim() + '\n\n';
            if (tag === 'b' || tag === 'strong') return '**' + inner.trim() + '**';
            if (tag === 'i' || tag === 'em') return '*' + inner.trim() + '*';
            if (tag === 'code') return '`' + inner.trim() + '`';
            
            return inner;
        }

        return walk(root).replace(/\[cite[^\]]*\]/g, '').replace(/\n{3,}/g, '\n\n').trim();
    }";
}
