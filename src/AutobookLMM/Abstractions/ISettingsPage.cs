using System.Threading.Tasks;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Contract for notebook settings and management.
/// </summary>
public interface ISettingsPage
{
    /// <summary>Gets the current URL of the page.</summary>
    Task<string> GetUrlAsync();

    /// <summary>
    /// Deletes the current notebook.
    /// </summary>
    Task DeleteNotebookAsync();

    /// <summary>
    /// Renames the current notebook.
    /// </summary>
    Task RenameNotebookAsync(string newName);

    /// <summary>
    /// Updates the system prompt (Custom Instructions) of the notebook.
    /// </summary>
    Task UpdateSystemPromptAsync(string prompt);
}
