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
    public async Task<string> SendMessageAsync(string message, IEnumerable<byte[]>? images = null, Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200)
    {
        await EnsureLoggedInAsync();
        return await _session.Chat.SendMessageAsync(message, images, onChunk, extractionScript, timeoutSeconds, pollingIntervalMs);
    }

    /// <inheritdoc />
    public async Task UploadSourcesAsync(List<string> filePaths)
    {
        await EnsureLoggedInAsync();
        await _session.Notebook.UploadSourcesAsync(filePaths);
    }

    /// <inheritdoc />
    public async Task<List<ChatMetadata>> ListChatsAsync()
    {
        await EnsureLoggedInAsync();
        return await _session.Chat.ListChatsAsync();
    }

    /// <inheritdoc />
    public async Task DeleteChatAsync(string title)
    {
        await EnsureLoggedInAsync();
        await _session.Chat.DeleteChatAsync(title);
    }

    /// <inheritdoc />
    public Task<bool> CheckLoginAsync() => _session.CheckLoginAsync();

    /// <inheritdoc />
    public Task OpenForLoginAsync() => _session.OpenForLoginAsync();

    /// <inheritdoc />
    public Task LoginWithCookiesAsync(string cookiesJson) => _session.LoginWithCookiesAsync(cookiesJson);

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
    public async Task<NotebookMetadata> InitializeNotebookAsync(string name, List<string> filePaths, string? systemInstructions = null)
    {
        await EnsureLoggedInAsync();

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
    public async Task OpenWorkspaceAsync(string notebookUrl)
    {
        await EnsureLoggedInAsync();

        // This will open/navigate 3 separate tabs: Notebook, Chat, and Settings
        await _session.PreloadProjectPagesAsync(notebookUrl);
    }

    /// <inheritdoc />
    public async Task<ChatMetadata> CreateNewChatAsync(string firstMessage, IEnumerable<byte[]>? images = null)
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
    public async Task ClearChatHistoryAsync()
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
    public async Task<ChatMetadata> RotateChatAsync(string oldChatTitle, string firstMessage, IEnumerable<byte[]>? images = null)
    {
        await EnsureLoggedInAsync();

        // 1. Open the new fresh chat
        var newChat = await _session.OpenChatAsync();

        // 2. Use the new chat tab to delete the old one
        await newChat.DeleteChatAsync(oldChatTitle);

        // 3. Send the first message
        await newChat.SendMessageAsync(firstMessage, images);

        // 4. Small delay to let Gemini update the conversation-title element
        await Task.Delay(1500);

        return new ChatMetadata
        {
            Title = await newChat.GetTitleAsync(),
            Url = await newChat.GetUrlAsync()
        };
    }

    /// <inheritdoc />
    public async Task UpdateSystemPromptAsync(string systemInstructions)
    {
        await EnsureLoggedInAsync();
        await _session.Settings.UpdateSystemPromptAsync(systemInstructions);
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
