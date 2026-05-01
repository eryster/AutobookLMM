using System;
using System.Threading.Tasks;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Top-level contract for an AutobookLMM browser session.
/// </summary>
public interface IGeminiSession : IAsyncDisposable
{
    /// <summary>The notebook tab: manages navigation, sources and notebook context.</summary>
    INotebookPage Notebook { get; }

    /// <summary>The chat tab: manages message submission and response extraction.</summary>
    INotebookChat Chat { get; }

    /// <summary>Opens a new independent chat tab for the active notebook.</summary>
    Task<INotebookChat> OpenChatAsync(string? url = null);

    /// <summary>Opens a new independent settings tab for the active notebook.</summary>
    Task<ISettingsPage> OpenSettingsAsync();

    /// <summary>Creates a high-level manager for orchestrating complex flows.</summary>
    ISettingsPage Settings { get; }

    /// <summary>Whether the persistent profile directory has data (login state indicator).</summary>
    bool IsProfileReady { get; }

    /// <summary>
    /// Checks if the user is already logged into Gemini.
    /// </summary>
    Task<bool> CheckLoginAsync();

    /// <summary>
    /// Opens the browser visibly for manual login. Returns once logged in.
    /// </summary>
    Task OpenForLoginAsync();

    /// <summary>
    /// Injects Google Cookies from a JSON string.
    /// </summary>
    Task LoginWithCookiesAsync(string cookiesJson);

    /// <summary>
    /// Changes whether the browser runs in headless mode.
    /// </summary>
    Task SetHeadlessAsync(bool headless);

    /// <summary>
    /// Captures a screenshot of the active page.
    /// </summary>
    Task CaptureDebugAsync(string label);

    /// <summary>
    /// Pre-loads and optimizes all 3 project tabs (Chat, Settings, Notebook) in parallel.
    /// </summary>
    Task PreloadProjectPagesAsync(string url);

    /// <summary>Sets the active notebook URL.</summary>
    void SetActiveNotebook(string url);

    /// <summary>Closes the active notebook pages and clears the state.</summary>
    Task CloseNotebookAsync();

    /// <summary>Wipes the persistent browser profile.</summary>
    Task PurgeProfileAsync();

    /// <summary>
    /// Creates a high-level manager instance for this session.
    /// </summary>
    IAutobookManager CreateManager();
}
