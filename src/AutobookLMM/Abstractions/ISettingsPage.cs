using System.Threading;
using System.Threading.Tasks;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Contract for notebook settings and management.
/// </summary>
public interface ISettingsPage : IBasePage
{
    /// <summary>
    /// Deletes the current notebook.
    /// </summary>
    Task DeleteNotebookAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the current notebook.
    /// </summary>
    Task RenameNotebookAsync(string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the system prompt (Custom Instructions) of the notebook.
    /// </summary>
    Task UpdateSystemPromptAsync(string prompt, CancellationToken cancellationToken = default);
}
