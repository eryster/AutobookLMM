using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Contract for notebook page interactions: navigation, sources, and context management.
/// </summary>
public interface INotebookPage : IBasePage
{
    // --- Notebook Navigation ---

    /// <summary>Gets the current notebook title from the UI.</summary>
    Task<string> GetTitleAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new notebook and returns its URL.</summary>
    Task<string> CreateAsync(string name, int account = 0, CancellationToken cancellationToken = default);

    /// <summary>Opens an existing notebook by its name (finds URL automatically).</summary>
    Task<string> OpenAsync(string name, int account = 0, CancellationToken cancellationToken = default);

    /// <summary>Lists all available notebook names for the given account.</summary>
    Task<List<string>> ListAsync(int account = 0, CancellationToken cancellationToken = default);

    /// <summary>Deletes an existing notebook by its URL.</summary>
    Task DeleteAsync(string notebookUrl, CancellationToken cancellationToken = default);

    /// <summary>Renames the current active notebook.</summary>
    Task RenameAsync(string newName, CancellationToken cancellationToken = default);

    // --- Sources Management ---

    /// <summary>Uploads physical files as sources to the notebook.</summary>
    Task UploadSourcesAsync(List<string> filePaths, CancellationToken cancellationToken = default);

    /// <summary>Deletes all sources from the current notebook.</summary>
    Task DeleteAllSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists all source titles currently in the notebook.</summary>
    Task<List<string>> ListSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the total number of sources currently in the notebook.</summary>
    Task<int> GetSourceCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a source by its title (or copy number).</summary>
    Task DeleteSourceAsync(string title, CancellationToken cancellationToken = default);
}
