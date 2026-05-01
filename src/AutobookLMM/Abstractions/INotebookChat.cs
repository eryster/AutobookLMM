using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutobookLMM.Models;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Contract for chat interactions: sending messages and extracting responses.
/// </summary>
public interface INotebookChat : IAsyncDisposable
{
    /// <summary>Gets the current URL of the chat page.</summary>
    Task<string> GetUrlAsync();

    /// <summary>
    /// Sends a message (optionally with images) and waits for the full response.
    /// To enable streaming, provide an onChunk callback.
    /// </summary>
    Task<string> SendMessageAsync(string message, IEnumerable<byte[]>? images = null, Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200);

    // --- Low-level methods ---

    /// <summary>Sends a message and optionally multiple images without waiting for the response.</summary>
    Task SubmitAsync(string message, IEnumerable<byte[]>? images = null);

    /// <summary>Waits for the current generation to finish and extracts the text.</summary>
    Task<string> GetResponseAsync(Action<string>? onChunk = null, string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200);

    /// <summary>Streams the current response chunks using IAsyncEnumerable.</summary>
    IAsyncEnumerable<string> StreamResponseAsync(string? extractionScript = null, int timeoutSeconds = 60, int pollingIntervalMs = 200);

    // --- Chat Management ---

    /// <summary>Lists all available chat conversations in the current notebook.</summary>
    Task<List<ChatMetadata>> ListChatsAsync();

    /// <summary>Deletes a specific chat conversation by its title.</summary>
    Task DeleteChatAsync(string title);
}
