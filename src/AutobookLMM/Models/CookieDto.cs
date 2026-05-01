namespace AutobookLMM.Models;

/// <summary>
/// Data transfer object for browser cookies.
/// </summary>
public class CookieDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public double? ExpirationDate { get; set; }
    public float? Expires { get; set; }
    public bool? Secure { get; set; }
    public bool? HttpOnly { get; set; }
    public string? SameSite { get; set; }
}
