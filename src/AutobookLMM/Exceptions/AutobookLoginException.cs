using System;

namespace AutobookLMM.Exceptions;

/// <summary>
/// Exception thrown when login or session operations fail.
/// </summary>
public class AutobookLoginException : AutobookException
{
    public AutobookLoginException(string message) : base(message) { }
    public AutobookLoginException(string message, Exception innerException) : base(message, innerException) { }
}
