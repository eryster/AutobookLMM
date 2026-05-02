using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Models;

namespace AutobookLMM.Abstractions;

/// <summary>
/// High-level manager for orchestrating complex NotebookLM workflows.
/// </summary>
public interface IAutobookManager : IAsyncDisposable
{
    /// <summary>The low-level notebook tab manager.</summary>
    INotebookPage Notebook { get; }

    /// <summary>The low-level chat tab manager.</summary>
    INotebookChat Chat { get; }

    /// <summary>The low-level settings tab manager.</summary>
    ISettingsPage Settings { get; }

    /// <summary>Whether the persistent profile directory has data (login state indicator).</summary>
    bool IsProfileReady { get; }

    /// <summary>
    /// Sends a message (optionally with images) to the active chat and waits for the full response.
    /// To enable streaming, provide an onChunk callback.
    /// </summary>
    Task<string> SendMessageAsync(string message, IEnumerable<byte[]>? images = null, Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pastes images directly into the active chat without submitting the message.
    /// </summary>
    Task PasteImagesAsync(IEnumerable<byte[]> images, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads the provided files as sources to the active notebook.
    /// </summary>
    Task UploadSourcesAsync(List<string> filePaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available conversations in the current notebook.
    /// </summary>
    Task<List<ChatMetadata>> ListChatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific chat conversation by its title.
    /// </summary>
    Task DeleteChatAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the user is already logged into Gemini.
    /// </summary>
    Task<bool> CheckLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the browser visibly for manual login. Returns once logged in.
    /// </summary>
    Task OpenForLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Injects Google Cookies from a JSON string.
    /// </summary>
    Task LoginWithCookiesAsync(string cookiesJson, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates a notebook, uploads sources, and sets system instructions in a single workflow.
    /// </summary>
    /// <param name="name">The name of the new notebook.</param>
    /// <param name="filePaths">List of local file paths to upload as sources.</param>
    /// <param name="systemInstructions">Optional custom instructions for the notebook.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Metadata of the initialized notebook.</returns>
    Task<NotebookMetadata> InitializeNotebookAsync(string name, List<string> filePaths, string? systemInstructions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an existing notebook into the session and prepares the workspace (Notebook page and an active Chat).
    /// </summary>
    /// <param name="notebookUrl">The URL of the existing notebook.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task OpenWorkspaceAsync(string notebookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes a new chat conversation by sending an initial message.
    /// This ensures the chat has a valid ID and auto-generated title.
    /// </summary>
    Task<ChatMetadata> CreateNewChatAsync(string firstMessage, IEnumerable<byte[]>? images = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all chat conversations in the current notebook.
    /// </summary>
    Task ClearChatHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a new chat, deletes the old one, and returns the new URL.
    /// </summary>
    /// <param name="oldChatTitle">Title of the chat to be deleted.</param>
    /// <param name="firstMessage">Mandatory first message to send to the new chat.</param>
    /// <param name="images">Optional list of image byte arrays to include in the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Metadata of the new fresh chat.</returns>
    Task<ChatMetadata> RotateChatAsync(string oldChatTitle, string firstMessage, IEnumerable<byte[]>? images = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates custom system instructions / prompt for the current notebook.
    /// </summary>
    /// <param name="systemInstructions">The instructions to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task UpdateSystemPromptAsync(string systemInstructions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an existing notebook by its URL.
    /// </summary>
    Task DeleteNotebookAsync(string notebookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the current active notebook.
    /// </summary>
    Task RenameNotebookAsync(string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all existing notebooks for the current account.
    /// </summary>
    Task<List<string>> ListNotebooksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a chat conversation with a specific title already exists in the current notebook.
    /// </summary>
    Task<bool> IsChatOpenAsync(string chatTitle, CancellationToken cancellationToken = default);
}
