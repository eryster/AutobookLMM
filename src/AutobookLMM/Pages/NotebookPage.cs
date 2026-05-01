using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Extensions;
using Microsoft.Playwright;

namespace AutobookLMM.Pages;

/// <summary>
/// Provides operations for navigating notebooks and managing their sources.
/// </summary>
public class NotebookPage(
    Func<Task<IPage>> pageFactory,
    SemaphoreSlim pageLock,
    Func<string, Task>? onDebug = null) : BasePage(pageFactory, pageLock, "notebook", onDebug), INotebookPage
{
    private const string NotebookTitleSelector = ".gds-title-l.title";
    private const string CreateBtnSelector = "[data-test-id=\"open-project-creation-window\"], [data-test-id=\"create-project-button\"]";
    private const string NameInputSelector = "#project-name-input";
    private const string EditSourcesBtnSelector = "[data-test-id=\"edit-sources-button\"]";
    private const string SourceTitleSelector = ".title.gds-title-s";
    private const string CloseSourcesBtnSelector = "[data-test-id=\"close-button\"]";
    private const string UploadButtonSelector = ".local-file-uploader-button";

    /// <inheritdoc />
    public Task<string> GetTitleAsync() =>
        RunAsync(async page =>
        {
            string[] selectors = [".gds-title-l.title", "header h1", ".notebook-title", ".title"];
            foreach (var selector in selectors)
            {
                try 
                {
                    var locator = page.Locator(selector).First;
                    if (await locator.IsVisibleAsync())
                    {
                        var text = await locator.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                    }
                } catch { }
            }

            var pageTitle = await page.TitleAsync();
            if (!string.IsNullOrWhiteSpace(pageTitle))
            {
                return pageTitle.Split('-')[0].Trim();
            }

            throw new Exception("Could not find notebook title on page.");
        });

    /// <inheritdoc />
    public Task<string> CreateAsync(string name, int account = 0) =>
        RunAsync(async page =>
        {
            await NavigateAsync(page, "https://gemini.google.com/notebooks/view", CreateBtnSelector);

            var btn = page.Locator(CreateBtnSelector).Last;
            await btn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await btn.ClickAsync();

            try
            {
                await page.WaitForSelectorAsync(NameInputSelector, new() { State = WaitForSelectorState.Visible, Timeout = 2000 });
            }
            catch
            {
                await btn.EvaluateAsync("el => el.click()");
                await page.WaitForSelectorAsync(NameInputSelector, new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }

            await page.FillVisibleAsync(NameInputSelector, name);
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForURLAsync(new Regex(@"/(u/\d+/)?notebook/.+"), new() { Timeout = 5000 });

            return page.Url;
        });

    /// <inheritdoc />
    public Task<string> OpenAsync(string name, int account = 0) =>
        RunAsync(async page =>
        {
            await NavigateAsync(page, $"https://gemini.google.com/notebooks/view{(account > 0 ? $"?u={account}" : "")}");

            var selector = $"[aria-label*=\"{name}\"], .notebook-list-item:has-text(\"{name}\")";
            var item = page.Locator(selector).First;

            if (await item.IsVisibleAsync())
            {
                await item.ClickAsync();
                await page.WaitForURLAsync(new Regex(@"/(u/\d+/)?notebook/.+"));
                return page.Url;
            }

            throw new Exception($"Notebook '{name}' not found.");
        });

    /// <inheritdoc />
    public Task<string> OpenUrlAsync(string url) =>
        RunAsync(async page =>
        {
            await NavigateAsync(page, url);

            var currentUrl = page.Url;
            var isNotebook = Regex.IsMatch(currentUrl, @"/(u/\d+/)?notebook/.+");

            if (isNotebook && !currentUrl.Contains("/app"))
            {
                return currentUrl;
            }

            throw new Exception("The notebook URL is invalid or inaccessible.");
        });

    /// <inheritdoc />
    public Task<List<string>> ListAsync(int account = 0) =>
        RunAsync(async page =>
        {
            await NavigateAsync(page, "https://gemini.google.com/notebooks/view", NotebookTitleSelector);
            await page.WaitForSelectorAsync(NotebookTitleSelector, new() { Timeout = 5000 });
            var elements = await page.QuerySelectorAllAsync(NotebookTitleSelector);
            var titles = new List<string>();
            foreach (var el in elements)
            {
                var text = await el.InnerTextAsync();
                if (!string.IsNullOrWhiteSpace(text)) titles.Add(text.Trim());
            }
            return titles;
        });

    /// <inheritdoc />
    public Task UploadSourcesAsync(List<string> filePaths) =>
        RunAsync(async page =>
        {
            if (filePaths == null || filePaths.Count == 0) return;

            if (!await page.IsCurrentlyVisibleAsync(CloseSourcesBtnSelector))
                await page.ClickVisibleAsync(EditSourcesBtnSelector);

            await page.Locator(UploadButtonSelector).First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

            var fileChooser = await page.RunAndWaitForFileChooserAsync(async () =>
            {
                await page.Locator(UploadButtonSelector).First.ClickAsync();
            });

            await fileChooser.SetFilesAsync(filePaths);

            var titlesLocator = page.Locator(SourceTitleSelector);
            for (int i = 0; i < 30; i++)
            {
                if (await titlesLocator.CountAsync() > 0) break;
                await Task.Delay(500);
            }
        });

    /// <inheritdoc />
    public Task DeleteSourceAsync(string title) =>
        RunAsync(async page =>
        {
            if (!await page.IsCurrentlyVisibleAsync(CloseSourcesBtnSelector))
            {
                await page.ClickVisibleAsync(EditSourcesBtnSelector);
                await page.WaitForSelectorAsync(CloseSourcesBtnSelector);
            }

            var pattern = int.TryParse(title, out _)
                ? $@"^Copied text \({title}\)$"
                : $@"^{Regex.Escape(title)}$";

            var titleLocator = page.Locator(SourceTitleSelector)
                .Filter(new() { HasTextRegex = new Regex(pattern, RegexOptions.IgnoreCase) }).First;

            var container = titleLocator.Locator("xpath=./ancestor::div[contains(@class, 'has-close-button')]").First;
            var removeBtn = container.Locator(".close-button button").First;

            await removeBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await removeBtn.ClickAsync();
            await titleLocator.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
        });

    /// <inheritdoc />
    public Task<List<string>> ListSourcesAsync() =>
        RunAsync(async page =>
        {
            if (!await page.IsCurrentlyVisibleAsync(CloseSourcesBtnSelector))
            {
                await page.ClickVisibleAsync(EditSourcesBtnSelector);
                await page.WaitForSelectorAsync(CloseSourcesBtnSelector);
            }
            return (await page.Locator(SourceTitleSelector).AllInnerTextsAsync())
                .Select(t => t.Trim())
                .ToList();
        });

    /// <inheritdoc />
    public Task<int> GetSourceCountAsync() =>
        RunAsync(async page =>
        {
            if (!await page.IsCurrentlyVisibleAsync(CloseSourcesBtnSelector))
            {
                await page.ClickVisibleAsync(EditSourcesBtnSelector);
                await page.WaitForSelectorAsync(CloseSourcesBtnSelector);
            }
            return await page.Locator(SourceTitleSelector).CountAsync();
        });

    /// <inheritdoc />
    public Task DeleteAllSourcesAsync() =>
        RunAsync(async page =>
        {
            if (!await page.IsCurrentlyVisibleAsync(CloseSourcesBtnSelector))
            {
                await page.ClickVisibleAsync(EditSourcesBtnSelector);
                await page.WaitForSelectorAsync(CloseSourcesBtnSelector);
            }

            await page.EvaluateAsync(@"() => {
                const selectors = ['.close-button button', 'button[aria-label*=""Remove""]', 'button[aria-label*=""Delet""]'];
                selectors.forEach(sel => {
                    document.querySelectorAll(sel).forEach(b => b.click());
                });
            }");

            var titlesLocator = page.Locator(SourceTitleSelector);
            for (int i = 0; i < 20; i++)
            {
                var count = await titlesLocator.CountAsync();
                if (count == 0) break;
                await Task.Delay(200);
            }
        });
}
