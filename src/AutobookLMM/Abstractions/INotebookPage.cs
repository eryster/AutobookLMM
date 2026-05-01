using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Contract for notebook page interactions: navigation, sources, and context management.
/// </summary>
public interface INotebookPage
{
    /// <summary>Gets the current URL of the page.</summary>
    Task<string> GetUrlAsync();

    // --- Notebook Navigation ---

    /// <summary>Gets the current notebook title from the UI.</summary>
    Task<string> GetTitleAsync();

    /// <summary>Creates a new notebook and returns its URL.</summary>
    Task<string> CreateAsync(string name, int account = 0);

    /// <summary>Opens an existing notebook by its direct URL.</summary>
    Task<string> OpenUrlAsync(string url);

    /// <summary>Opens an existing notebook by its name (finds URL automatically).</summary>
    Task<string> OpenAsync(string name, int account = 0);

    /// <summary>Lists all available notebook names for the given account.</summary>
    Task<List<string>> ListAsync(int account = 0);

    // --- Sources Management ---

    /// <summary>Uploads physical files as sources to the notebook.</summary>
    Task UploadSourcesAsync(List<string> filePaths);

    /// <summary>Deletes all sources from the current notebook.</summary>
    Task DeleteAllSourcesAsync();

    /// <summary>Lists all source titles currently in the notebook.</summary>
    Task<List<string>> ListSourcesAsync();

    /// <summary>Removes a source by its title (or copy number).</summary>
    Task DeleteSourceAsync(string title);
}
