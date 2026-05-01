using System.Collections.Generic;
using System.Threading.Tasks;
using AutobookLMM.Models;

namespace AutobookLMM.Abstractions;

/// <summary>
/// High-level manager for orchestrating complex NotebookLM workflows.
/// </summary>
public interface IAutobookManager
{
    /// <summary>
    /// Creates a notebook, uploads sources, and sets system instructions in a single workflow.
    /// </summary>
    /// <param name="name">The name of the new notebook.</param>
    /// <param name="filePaths">List of local file paths to upload as sources.</param>
    /// <param name="systemInstructions">Optional custom instructions for the notebook.</param>
    /// <returns>Metadata of the initialized notebook.</returns>
    Task<NotebookMetadata> InitializeNotebookAsync(string name, List<string> filePaths, string? systemInstructions = null);

    /// <summary>
    /// Loads an existing notebook into the session and prepares the workspace (Notebook page and an active Chat).
    /// </summary>
    /// <param name="notebookUrl">The URL of the existing notebook.</param>
    Task OpenWorkspaceAsync(string notebookUrl);

    /// <summary>
    /// Deletes all chat conversations in the current notebook.
    /// </summary>
    Task ClearChatHistoryAsync();

    /// <summary>
    /// Opens a new chat, deletes the old one, and returns the new URL.
    /// </summary>
    /// <param name="oldChatTitle">Title of the chat to be deleted.</param>
    /// <returns>Metadata of the new fresh chat.</returns>
    Task<ChatMetadata> RotateChatAsync(string oldChatTitle);
}
