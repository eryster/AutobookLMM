using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutobookLMM.Exceptions;
using Microsoft.Playwright;

namespace AutobookLMM.Validators;

/// <summary>
/// Static validator for notebook source files.
/// </summary>
public static class SourceValidator
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".csv", ".txt", ".md", ".markdown",
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".heif", ".heic", ".mp3"
    };

    /// <summary>
    /// Validates if local file paths exist and have valid, supported extensions.
    /// Throws AutobookException if validation fails.
    /// </summary>
    /// <param name="filePaths">The list of local file paths to validate.</param>
    public static void Validate(IEnumerable<string> filePaths)
    {
        if (filePaths == null) throw new ArgumentNullException(nameof(filePaths));

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new AutobookException("The file path cannot be empty.");
            }

            if (!File.Exists(path))
            {
                throw new AutobookException($"The file was not found at the specified path: {path}");
            }

            var extension = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(extension))
            {
                throw new AutobookException($"The file extension '{extension}' of file '{Path.GetFileName(path)}' is not supported by NotebookLM.");
            }
        }
    }
}
