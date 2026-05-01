using System.Linq;

namespace AutobookLMM.Models;

/// <summary>
/// Rich information about a specific chat conversation.
/// </summary>
public class ChatMetadata
{
    /// <summary>The title of the conversation.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The navigation URL of this specific chat.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The unique identifier of the chat conversation.</summary>
    public string Id => Url.Split('/').LastOrDefault() ?? string.Empty;
}
