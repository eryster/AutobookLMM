using System;

namespace AutobookLMM.Exceptions;

/// <summary>
/// Exception thrown when navigation to a specific notebook or chat fails.
/// </summary>
public class AutobookNavigationException : AutobookException
{
    public AutobookNavigationException(string message) : base(message) { }
    public AutobookNavigationException(string message, Exception innerException) : base(message, innerException) { }
}
