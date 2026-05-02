using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Core;
using AutobookLMM.Models;

namespace AutobookLMM.Managers;

/// <summary>
/// Orchestrates multiple page-level operations into high-level workflows.
/// </summary>
public class AutobookManager : IAutobookManager
{
    private readonly IGeminiSession _session;
    private readonly bool _ownsSession;
    private bool? _isLoggedInCached;

    public INotebookPage Notebook => _session.Notebook;
    public INotebookChat Chat => _session.Chat;
    public ISettingsPage Settings => _session.Settings;

    /// <inheritdoc />
    public bool IsProfileReady => _session.IsProfileReady;

    /// <inheritdoc />
    public async Task<string> SendMessageAsync(string message, IEnumerable<byte[]>? images = null, Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        string? chatTitle = null;
        try
        {
            chatTitle = await _session.Chat.GetTitleAsync();
        }
        catch { }

        try
        {
            return await _session.Chat.SendMessageAsync(message, images, onChunk, extractionScript, timeoutSeconds, pollingIntervalMs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutobookManager] Error sending message: {ex.Message}. Recovering chat and retrying...");
            var url = GeminiSession.CurrentNotebookUrl;
            if (string.IsNullOrEmpty(url)) throw;

            try { await _session.CloseChatAsync(); } catch { }

            if (!string.IsNullOrEmpty(chatTitle) && chatTitle != "Untitled Conversation")
            {
                bool opened = await _session.Chat.OpenChatByTitleAsync(chatTitle);
                if (!opened)
                {
                    Console.WriteLine($"[AutobookManager] Chat with title '{chatTitle}' could not be opened.");
                }
            }

            await Task.Delay(2000);

            return await _session.Chat.SendMessageAsync(message, images, onChunk, extractionScript, timeoutSeconds, pollingIntervalMs);
        }
    }

    /// <inheritdoc />
    public async Task PasteImagesAsync(IEnumerable<byte[]> images, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        await _session.Chat.PasteImagesAsync(images);
    }

    /// <inheritdoc />
    public async Task UploadSourcesAsync(List<string> filePaths, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        AutobookLMM.Validators.SourceValidator.Validate(filePaths);
        await _session.Notebook.UploadSourcesAsync(filePaths);
    }

    /// <inheritdoc />
    public async Task<List<ChatMetadata>> ListChatsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        return await _session.Chat.ListChatsAsync();
    }

    /// <inheritdoc />
    public async Task DeleteChatAsync(string title, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        await _session.Chat.DeleteChatAsync(title);
    }

    /// <inheritdoc />
    public Task<bool> CheckLoginAsync(CancellationToken cancellationToken = default) => _session.CheckLoginAsync();

    /// <inheritdoc />
    public Task OpenForLoginAsync(CancellationToken cancellationToken = default) => _session.OpenForLoginAsync();

    /// <inheritdoc />
    public Task LoginWithCookiesAsync(string cookiesJson, CancellationToken cancellationToken = default) => _session.LoginWithCookiesAsync(cookiesJson);

    public AutobookManager(bool? headless = null, string? cookiesJson = null)
    {
        _session = new GeminiSession(headless);
        _ownsSession = true;

        if (!string.IsNullOrEmpty(cookiesJson))
        {
            string json = cookiesJson.Trim();
            if (!json.StartsWith('[') && !json.StartsWith('{') && File.Exists(json))
            {
                json = File.ReadAllText(json);
            }
            _session.LoginWithCookiesAsync(json).GetAwaiter().GetResult();
        }
    }

    public AutobookManager(AutobookOptions options)
    {
        _session = new GeminiSession(options ?? throw new ArgumentNullException(nameof(options)));
        _ownsSession = true;
    }

    public AutobookManager(IGeminiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _ownsSession = false;
    }

    private async Task EnsureLoggedInAsync()
    {
        if (!_session.IsProfileReady)
        {
            throw new InvalidOperationException("User profile is not ready. Please login first using OpenForLoginAsync() or LoginWithCookiesAsync().");
        }

        if (_isLoggedInCached == true) return;

        var loggedIn = await _session.CheckLoginAsync();
        if (!loggedIn)
        {
            throw new InvalidOperationException("User is not logged into Gemini. Please login first using OpenForLoginAsync() or LoginWithCookiesAsync().");
        }

        _isLoggedInCached = true;
    }

    /// <inheritdoc />
    public async Task<NotebookMetadata> InitializeNotebookAsync(string name, List<string> filePaths, string? systemInstructions = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();

        if (filePaths is { Count: > 0 })
        {
            AutobookLMM.Validators.SourceValidator.Validate(filePaths);
        }

        // 1. Create the notebook
        var url = await _session.Notebook.CreateAsync(name);

        // 2. Upload sources if any
        if (filePaths is { Count: > 0 })
        {
            await _session.Notebook.UploadSourcesAsync(filePaths);
        }

        // 3. Set system instructions if provided
        if (!string.IsNullOrWhiteSpace(systemInstructions))
        {
            // The notebook page usually shares the same context as settings for the active notebook
            // but we ensure the session knows which notebook is active if needed.
            _session.SetActiveNotebook(url);

            // Navigate settings to the correct place
            await _session.Settings.UpdateSystemPromptAsync(systemInstructions);
        }

        return new NotebookMetadata
        {
            Name = name,
            Url = url
        };
    }

    /// <inheritdoc />
    public async Task OpenWorkspaceAsync(string notebookUrl, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();

        // This will open/navigate 3 separate tabs: Notebook, Chat, and Settings
        await _session.PreloadProjectPagesAsync(notebookUrl);
    }

    /// <inheritdoc />
    public async Task<ChatMetadata> CreateNewChatAsync(string firstMessage, IEnumerable<byte[]>? images = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();

        // 1. Find or create the target chat instance
        INotebookChat targetChat;
        var mainChatUrl = await _session.Chat.GetUrlAsync();

        if (!mainChatUrl.Contains("/app/"))
        {
            targetChat = _session.Chat;
        }
        else
        {
            targetChat = await _session.OpenChatAsync();
        }

        // 2. Send the first message and wait for completion (to ensure title is generated)
        await targetChat.SendMessageAsync(firstMessage, images);

        // 3. Small delay to let Gemini update the conversation-title element
        await Task.Delay(1500);

        return new ChatMetadata
        {
            Title = await targetChat.GetTitleAsync(),
            Url = await targetChat.GetUrlAsync()
        };
    }

    /// <inheritdoc />
    public async Task ClearChatHistoryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();

        // We open a temporary background chat tab to perform the cleanup.
        // This ensures any active user conversation in another tab is NOT disturbed.
        await using var worker = await _session.OpenChatAsync();

        var chats = await worker.ListChatsAsync();

        foreach (var chat in chats)
        {
            await worker.DeleteChatAsync(chat.Title);
        }
    }

    /// <inheritdoc />
    public async Task<ChatMetadata> RotateChatAsync(string oldChatTitle, string firstMessage, IEnumerable<byte[]>? images = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();

        // 0. Close the previous chat tab first to free up the tab
        try
        {
            await _session.Chat.DisposeAsync();
        }
        catch { }

        // 1. Open the new fresh chat
        var newChat = await _session.OpenChatAsync();

        // 2. Use the new chat tab to delete the old one
        try
        {
            await newChat.DeleteChatAsync(oldChatTitle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RotateChatAsync] Warning: Could not delete old chat '{oldChatTitle}', continuing anyway: {ex.Message}");
        }

        // 3. Small delay for stability before sending the first message
        await Task.Delay(3000);

        // 4. Send the first message
        var responseText = await newChat.SendMessageAsync(firstMessage, images);

        // 5. Small delay to let Gemini update the conversation-title element
        await Task.Delay(1500);

        return new ChatMetadata
        {
            Title = await newChat.GetTitleAsync(),
            Url = await newChat.GetUrlAsync(),
            LastResponse = responseText
        };
    }

    /// <inheritdoc />
    public async Task UpdateSystemPromptAsync(string systemInstructions, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        await _session.Settings.UpdateSystemPromptAsync(systemInstructions);
    }

    /// <inheritdoc />
    public async Task DeleteNotebookAsync(string notebookUrl, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        await _session.Notebook.DeleteAsync(notebookUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RenameNotebookAsync(string newName, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        await _session.Notebook.RenameAsync(newName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<string>> ListNotebooksAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        return await _session.Notebook.ListAsync(0, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsChatOpenAsync(string chatTitle, CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync();
        var chats = await _session.Chat.ListChatsAsync();
        return chats.Any(c => string.Equals(c.Title, chatTitle, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsSession && _session != null)
        {
            await _session.DisposeAsync();
        }
    }
}
