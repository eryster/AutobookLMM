using System;

namespace AutobookLMM.Exceptions;

/// <summary>
/// Base exception for the AutobookLMM library.
/// </summary>
public class AutobookException : Exception
{
    public AutobookException(string message) : base(message) { }
    public AutobookException(string message, Exception innerException) : base(message, innerException) { }
}
