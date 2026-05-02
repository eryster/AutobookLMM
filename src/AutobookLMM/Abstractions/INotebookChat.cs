using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Models;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Contract for chat interactions: sending messages and extracting responses.
/// </summary>
public interface INotebookChat : IBasePage, IAsyncDisposable
{
    /// <summary>Gets the auto-generated title of the current conversation.</summary>
    Task<string> GetTitleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message (optionally with images) and waits for the full response.
    /// To enable streaming, provide an onChunk callback.
    /// </summary>
    Task<string> SendMessageAsync(string message, IEnumerable<byte[]>? images = null, Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200, CancellationToken cancellationToken = default);

    // --- Low-level methods ---

    /// <summary>Sends a message and optionally multiple images without waiting for the response.</summary>
    Task SubmitAsync(string message, IEnumerable<byte[]>? images = null, CancellationToken cancellationToken = default);

    /// <summary>Types message directly into input field.</summary>
    Task TypeMessageAsync(string text, bool pressEnter = false, IEnumerable<byte[]>? images = null, CancellationToken cancellationToken = default);

    /// <summary>Pastes images directly into the input field without sending the message.</summary>
    Task PasteImagesAsync(IEnumerable<byte[]> images, CancellationToken cancellationToken = default);

    /// <summary>Waits for the current generation to finish and extracts the text.</summary>
    Task<string> GetResponseAsync(Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200, CancellationToken cancellationToken = default);

    /// <summary>Streams the current response chunks using IAsyncEnumerable.</summary>
    IAsyncEnumerable<string> StreamResponseAsync(string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200, CancellationToken cancellationToken = default);

    // --- Chat Management ---

    /// <summary>Lists all available chat conversations in the current notebook.</summary>
    Task<List<ChatMetadata>> ListChatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific chat conversation by its title.</summary>
    Task DeleteChatAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>Opens a specific chat conversation by its title from the summary view.</summary>
    Task<bool> OpenChatByTitleAsync(string title, CancellationToken cancellationToken = default);
}
