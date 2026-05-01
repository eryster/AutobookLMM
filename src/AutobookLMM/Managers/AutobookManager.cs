using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Models;

namespace AutobookLMM.Managers;

/// <summary>
/// Orchestrates multiple page-level operations into high-level workflows.
/// </summary>
public class AutobookManager(IGeminiSession session) : IAutobookManager
{
    private readonly IGeminiSession _session = session;

    /// <inheritdoc />
    public async Task<NotebookMetadata> InitializeNotebookAsync(string name, List<string> filePaths, string? systemInstructions = null)
    {
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
        // This will open/navigate 3 separate tabs: Notebook, Chat, and Settings
        await _session.PreloadProjectPagesAsync(notebookUrl);
    }

    /// <inheritdoc />
    public async Task<ChatMetadata> CreateNewChatAsync(string firstMessage, IEnumerable<byte[]>? images = null)
    {
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
    public async Task<ChatMetadata> RotateChatAsync(string oldChatTitle)
    {
        // 1. Open the new fresh chat
        var newChat = await _session.OpenChatAsync();

        // 2. Use the new chat tab to delete the old one
        await newChat.DeleteChatAsync(oldChatTitle);

        // 3. Return the new Metadata
        return new ChatMetadata
        {
            Title = "New Chat", // We don't know the title until a message is sent, but we have the URL
            Url = await newChat.GetUrlAsync()
        };
    }
}
