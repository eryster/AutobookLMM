using System;
using System.Threading;
using System.Threading.Tasks;
using AutobookLMM.Abstractions;
using AutobookLMM.Managers;
using AutobookLMM.Models;

namespace AutobookLMM.Core;

/// <summary>
/// A fluent builder for configuring and creating AutobookManager.
/// </summary>
public class AutobookBuilder
{
    private readonly AutobookOptions _options = new();
    private string? _preloadedWorkspaceUrl;

    /// <summary>
    /// Starts a new builder instance.
    /// </summary>
    public static AutobookBuilder Create() => new();

    /// <summary>
    /// Configures headless mode.
    /// </summary>
    public AutobookBuilder WithHeadless(bool headless)
    {
        _options.Headless = headless;
        return this;
    }

    /// <summary>
    /// Configures session cookies as a JSON string or path to a JSON file.
    /// </summary>
    public AutobookBuilder WithCookies(string cookiesJson)
    {
        _options.CookiesJson = cookiesJson;
        return this;
    }

    /// <summary>
    /// Configures a notebook workspace URL to preload immediately upon building.
    /// </summary>
    /// <param name="notebookUrl">The URL of the existing notebook.</param>
    public AutobookBuilder WithPreloadedWorkspace(string notebookUrl)
    {
        _preloadedWorkspaceUrl = notebookUrl;
        return this;
    }

    /// <summary>
    /// Builds the AutobookManager instance.
    /// </summary>
    public IAutobookManager Build()
    {
        return new AutobookManager(_options);
    }

    /// <summary>
    /// Builds the AutobookManager instance asynchronously and preloads the workspace if configured.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IAutobookManager> BuildAsync(CancellationToken cancellationToken = default)
    {
        var manager = new AutobookManager(_options);
        if (!string.IsNullOrEmpty(_preloadedWorkspaceUrl))
        {
            await manager.OpenWorkspaceAsync(_preloadedWorkspaceUrl, cancellationToken);
        }
        return manager;
    }
}
