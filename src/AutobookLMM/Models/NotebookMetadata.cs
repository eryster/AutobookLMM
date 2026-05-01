using System.Linq;

namespace AutobookLMM.Models;

/// <summary>
/// Rich information about a NotebookLM project.
/// </summary>
public class NotebookMetadata
{
    /// <summary>The display name of the notebook.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The full navigation URL of the notebook.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The unique identifier extracted from the URL.</summary>
    public string Id => Url.Split('/').LastOrDefault() ?? string.Empty;
}
