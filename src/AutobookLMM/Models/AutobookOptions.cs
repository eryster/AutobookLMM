namespace AutobookLMM.Models;

/// <summary>
/// Configuration options for the AutobookLMM library.
/// </summary>
public class AutobookOptions
{
    /// <summary>Gets or sets whether the browser runs in headless mode. Defaults to false.</summary>
    public bool Headless { get; set; } = false;

    /// <summary>Gets or sets the session cookies as a JSON string or path to a JSON file.</summary>
    public string? CookiesJson { get; set; }
}
