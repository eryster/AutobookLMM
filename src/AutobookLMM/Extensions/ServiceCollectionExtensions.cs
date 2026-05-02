using System;
using AutobookLMM.Abstractions;
using AutobookLMM.Core;
using AutobookLMM.Managers;
using AutobookLMM.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AutobookLMM.Extensions;

/// <summary>
/// Service collection extensions for registering AutobookLMM.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AutobookLMM services to the service collection.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddAutobookLMM(options => {
    ///     options.Headless = true;
    ///     options.CookiesJson = "cookies.json";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAutobookLMM(this IServiceCollection services, Action<AutobookOptions>? configure = null)
    {
        var options = new AutobookOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddTransient<IGeminiSession>(sp => new GeminiSession(sp.GetRequiredService<AutobookOptions>()));
        services.AddTransient<IAutobookManager, AutobookManager>();

        return services;
    }
}
