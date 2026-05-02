using System.Threading;
using System.Threading.Tasks;

namespace AutobookLMM.Abstractions;

/// <summary>
/// Base contract for all page-level operations.
/// </summary>
public interface IBasePage
{
    /// <summary>Gets the current URL of the page.</summary>
    Task<string> GetUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>Navigates the page to the specific URL.</summary>
    Task NavigateToUrlAsync(string url, CancellationToken cancellationToken = default);
}
