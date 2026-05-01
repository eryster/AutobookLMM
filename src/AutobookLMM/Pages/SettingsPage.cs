using System;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Extensions;
using Microsoft.Playwright;

namespace AutobookLMM.Pages;

public class SettingsPage(
    Func<Task<IPage>> pageFactory,
    SemaphoreSlim pageLock,
    Func<string, Task>? onDebug = null) : BasePage(pageFactory, pageLock, "settings", onDebug), ISettingsPage
{
    private const string MenuBtnSelector = "[data-test-id=\"project-settings-menu\"]";
    private const string MenuContentSelector = "mat-mdc-menu-content, .mat-mdc-menu-content";
    private const string GlobalSaveBtnSelector = "[data-test-id=\"save-button\"]";

    private const string RenameOptionBtnSelector = "[data-test-id=\"rename-project\"]";
    private const string RenameInputSelector = "[data-test-id=\"edit-title-input\"]";

    private const string InstrEditBtnSelector = "[data-test-id=\"edit-project-instructions\"]";
    private const string InstrTextareaSelector = "[data-test-id=\"textarea\"]";
    private const string CancelBtnSelector = "[data-test-id=\"cancel-button\"]";

    private const string DeleteOptionBtnSelector = "[data-test-id=\"delete-project\"]";
    private const string ConfirmDeleteBtnSelector = "[data-test-id=\"confirm-button\"]";

    public Task DeleteNotebookAsync() =>
        RunAsync(async page =>
        {
            await EnsureMenuOpenAsync(page);
            await page.ClickVisibleAsync(DeleteOptionBtnSelector);
            await page.ClickVisibleAsync(ConfirmDeleteBtnSelector, 5000);
            await page.WaitForURLAsync("**/notebooks/view**", new() { Timeout = 5000 });
        });

    public Task RenameNotebookAsync(string newName) =>
        RunAsync(async page =>
        {
            await EnsureMenuOpenAsync(page);
            await page.ClickVisibleAsync(RenameOptionBtnSelector);

            var input = page.Locator(RenameInputSelector);
            await input.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            await input.FillAsync(newName);
            await Task.Delay(200);

            var saveBtn = page.Locator(GlobalSaveBtnSelector);
            if (await saveBtn.IsVisibleAsync())
            {
                await page.ClickVisibleAsync(GlobalSaveBtnSelector);
                await input.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
            }
        });

    public Task UpdateSystemPromptAsync(string prompt) =>
        RunAsync(async page =>
        {
            await EnsureMenuOpenAsync(page);
            await page.ClickVisibleAsync(InstrEditBtnSelector);

            var textarea = page.Locator(InstrTextareaSelector);
            await textarea.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            await textarea.FocusAsync();
            await textarea.FillAsync(prompt);

            await Task.Delay(1000);

            var saveBtn = page.Locator(GlobalSaveBtnSelector).Last;

            if (await saveBtn.IsVisibleAsync() && await saveBtn.IsEnabledAsync())
            {
                await page.ClickVisibleAsync(GlobalSaveBtnSelector);
                try
                {
                    await textarea.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
                }
                catch
                {
                    if (await saveBtn.IsVisibleAsync()) await page.ClickVisibleAsync(GlobalSaveBtnSelector);
                    await textarea.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
                }
            }
            else
            {
                await page.ClickVisibleAsync(CancelBtnSelector);
            }
        });

    private async Task EnsureMenuOpenAsync(IPage page)
    {
        if (!await page.IsCurrentlyVisibleAsync(MenuContentSelector))
        {
            await page.ClickVisibleAsync(MenuBtnSelector);
            await page.WaitForSelectorAsync(MenuContentSelector, new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
        }
    }
}
